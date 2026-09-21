using System.Net;
using System.Net.Sockets;
using System.Text;
using DlnaServer.Core.Configuration;
using DlnaServer.Upnp.Ssdp;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Upnp.Discovery
{
    /// <summary>
    /// Announces the server on the network so renderers can discover it.
    /// </summary>
    /// <remarks>
    /// One socket per interface, seven notification types each, sent to the SSDP multicast group and -
    /// because the reference does and some networks drop multicast - to the limited broadcast address too.
    /// <para>
    /// A send failure does not restart the process. The reference tore the whole host down and rebuilt it
    /// when a datagram failed, which its own logs show happening; a transient network blip should not cost
    /// the server its index and every open stream.
    /// </para>
    /// </remarks>
    internal sealed partial class SsdpNotifierHostedService : BackgroundService
    {
        private const string AliveSubtype = "ssdp:alive";
        private const string ByebyeSubtype = "ssdp:byebye";
        private const int MulticastTimeToLive = 10;

        private const int MinimumAliveIntervalSeconds = 5;
        private const int DefaultAliveIntervalSeconds = 30;

        private readonly IUpnpDeviceRegistry _devices;
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly ILogger<SsdpNotifierHostedService> _logger;
        private readonly string _serverSignature;

        // Last interval that was readable. Touched only by the single announce loop, so it needs no
        // synchronisation - see ResolveInterval for why it is kept at all.
        private TimeSpan _interval = TimeSpan.FromSeconds(DefaultAliveIntervalSeconds);

        public SsdpNotifierHostedService(
            IUpnpDeviceRegistry devices,
            IOptionsMonitor<DlnaOptions> options,
            ILogger<SsdpNotifierHostedService> logger)
        {
            _devices = devices;
            _options = options;
            _logger = logger;

            _serverSignature = UpnpServerSignature.Create(
                typeof(SsdpNotifierHostedService).Assembly.GetName().Version ?? new Version(1, 0),
                options.CurrentValue.Server.Port);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Not a return. This used to end the service the first time no interface was found, which on
            // a NAS is routine rather than terminal: the process can start before DHCP hands out a
            // lease, and the server was then unadvertised for the life of the process while looking
            // perfectly healthy - with every res@url falling back to loopback. Re-resolved each interval
            // instead, so a late lease and a changed lease both recover on their own.
            if (_devices.Identities.Count == 0)
            {
                LogNoInterfaces();
            }
            else
            {
                LogAdvertisingAll();
            }

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        if (_devices.Refresh())
                        {
                            LogInterfacesChanged(_devices.Identities.Count);
                            LogAdvertisingAll();
                        }

                        await AnnounceAsync(AliveSubtype, stoppingToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        // Guarded per iteration, not around the loop. Anything escaping here ends
                        // ExecuteAsync, and BackgroundServiceExceptionBehavior.StopHost does not rethrow -
                        // so the server either dies with exit code 0 or, worse, keeps running while never
                        // advertising again and quietly vanishes from every television at the next
                        // max-age. One failed datagram must cost one interval, nothing more.
                        LogAnnounceFailed(exception);
                    }

                    await Task.Delay(ResolveInterval(), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
            finally
            {
                try
                {
                    // Tell renderers the server is going away, so it disappears from their lists promptly
                    // instead of lingering until max-age expires.
                    // shutdown must still announce itself even though the stopping token is already set
                    await AnnounceAsync(ByebyeSubtype, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    // In a finally during shutdown: a disposed socket here would replace a clean stop
                    // with an unhandled exception, and the courtesy byebye is not worth that.
                    LogByebyeFailed(exception);
                }
            }
        }

        private void LogAdvertisingAll()
        {
            foreach (var identity in _devices.Identities)
            {
                LogAdvertising(identity.Address.ToString(), identity.DeviceId);
            }
        }

        /// <summary>
        /// The configured announce interval, or the last one that was readable.
        /// </summary>
        /// <remarks>
        /// Reading <c>CurrentValue</c> re-runs <c>DlnaOptionsValidator</c> whenever <c>config.json</c> has
        /// changed, so an operator saving an invalid value from the admin UI makes this throw
        /// <see cref="OptionsValidationException"/> - on a line that used to sit outside any guard, which
        /// stopped announcements permanently. Falling back to the last good interval keeps the server
        /// discoverable while the configuration is broken, which is exactly when an operator needs to
        /// reach the admin UI to fix it.
        /// </remarks>
        private TimeSpan ResolveInterval()
        {
            try
            {
                _interval = TimeSpan.FromSeconds(
                    Math.Max(
                        MinimumAliveIntervalSeconds,
                        _options.CurrentValue.Compatibility.SsdpAliveIntervalInSeconds));
            }
            catch (OptionsValidationException exception)
            {
                LogIntervalUnavailable(exception.Message, _interval.TotalSeconds);
            }

            return _interval;
        }

        private async Task AnnounceAsync(string subtype, CancellationToken cancellationToken)
        {
            var compatibility = _options.CurrentValue.Compatibility;

            foreach (var identity in _devices.Identities)
            {
                using var client = TryCreateClient(identity.Address);

                if (client is null)
                {
                    continue;
                }

                foreach (var notificationType in SsdpAdvertisement.NotificationTypesFor(identity))
                {
                    await SendAsync(client, identity, subtype, notificationType, multicast: true, cancellationToken);

                    if (compatibility.AlsoNotifyBroadcastAddress)
                    {
                        await SendAsync(client, identity, subtype, notificationType, multicast: false, cancellationToken);
                    }
                }
            }
        }

        private async Task SendAsync(
            UdpClient client,
            UpnpDeviceIdentity identity,
            string subtype,
            string notificationType,
            bool multicast,
            CancellationToken cancellationToken)
        {
            var destination = multicast
                ? new IPEndPoint(IPAddress.Parse(SsdpMessageBuilder.MulticastAddress), SsdpMessageBuilder.Port)
                : new IPEndPoint(IPAddress.Broadcast, SsdpMessageBuilder.Port);

            var message = SsdpMessageBuilder.CreateNotify(
                $"{destination.Address}:{destination.Port}",
                identity.DescriptionUrl,
                _serverSignature,
                subtype,
                notificationType,
                SsdpMessageBuilder.CreateUniqueServiceName(identity.DeviceId, notificationType));

            var bytes = Encoding.UTF8.GetBytes(message);

            try
            {
                _ = await client.SendAsync(bytes, destination, cancellationToken);
            }
            catch (SocketException exception)
            {
                // Logged and carried on. A failed datagram is a transient network condition, not a
                // reason to stop advertising or to restart the server.
                LogSendFailed(identity.Address.ToString(), exception.Message);
            }
        }

        private UdpClient? TryCreateClient(IPAddress address)
        {
            try
            {
                var client = new UdpClient(AddressFamily.InterNetwork);

                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.ExclusiveAddressUse = false;
                client.Client.Bind(new IPEndPoint(address, port: 0));
                client.Ttl = MulticastTimeToLive;
                client.EnableBroadcast = true;
                client.Client.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.MulticastTimeToLive,
                    MulticastTimeToLive);

                return client;
            }
            catch (SocketException exception)
            {
                LogSocketFailed(address.ToString(), exception.Message);
                return null;
            }
        }
    }
}
