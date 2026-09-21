using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Text;
using DlnaServer.Core.Configuration;
using DlnaServer.Upnp.Ssdp;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Upnp.Discovery
{
    /// <summary>
    /// Answers <c>M-SEARCH</c> discovery requests from renderers.
    /// </summary>
    /// <remarks>
    /// Listens on the SSDP multicast group and replies unicast to whoever asked. Search-target matching
    /// is corrected here relative to the reference - see <see cref="SsdpAdvertisement.ShouldAnswer"/>.
    /// </remarks>
    internal sealed partial class SsdpListenerHostedService : BackgroundService
    {
        private const string SearchMethod = "M-SEARCH";
        private const string DiscoverHeader = "\"ssdp:discover\"";

        // Back off, rather than give up, once failures stop looking like one stray datagram.
        private const int MaxConsecutiveReceiveFailures = 10;

        // Long enough for a network blip or a switch reboot to finish, short enough that a television
        // searching again finds the server. Paired with the counter above rather than replacing it: ten
        // fast failures mean the socket is unusable right now, not that it is unusable for good.
        private static readonly TimeSpan _receiveBackoff = TimeSpan.FromSeconds(5);

        // Concurrent replies waiting out their MX delay. Each is a pending timer and ~150 bytes, so the
        // cap is about refusing a flood rather than saving memory: past this the traffic is not a
        // household and SSDP is best-effort, so dropping a search is better than queueing a reply
        // nothing is still listening for.
        private const int MaxConcurrentReplies = 64;

        // MX is capped at 5 seconds by the UPnP specification; 1 is what a searcher that omits it gets.
        private const int MaxSearchDelaySeconds = 5;
        private const int DefaultSearchDelaySeconds = 1;

        // SIO_UDP_CONNRESET. Windows-only ioctl that stops a previous send's ICMP port-unreachable from
        // surfacing as an error on the next receive; Linux neither raises that nor supports the call.
        private const int SioUdpConnectionReset = -1_744_830_452;

        private readonly IUpnpDeviceRegistry _devices;
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SsdpListenerHostedService> _logger;
        private readonly string _serverSignature;
        private readonly SemaphoreSlim _replySlots = new(MaxConcurrentReplies, MaxConcurrentReplies);

        public SsdpListenerHostedService(
            IUpnpDeviceRegistry devices,
            IOptionsMonitor<DlnaOptions> options,
            TimeProvider timeProvider,
            ILogger<SsdpListenerHostedService> logger)
        {
            _devices = devices;
            _options = options;
            _timeProvider = timeProvider;
            _logger = logger;

            _serverSignature = UpnpServerSignature.Create(
                typeof(SsdpListenerHostedService).Assembly.GetName().Version ?? new Version(1, 0),
                options.CurrentValue.Server.Port);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var client = TryCreateListener();

            if (client is null)
            {
                return;
            }

            LogListening(SsdpMessageBuilder.Port);

            var consecutiveFailures = 0;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    UdpReceiveResult received;

                    try
                    {
                        received = await client.ReceiveAsync(stoppingToken);
                        consecutiveFailures = 0;
                    }
                    catch (SocketException exception)
                    {
                        // A single failed receive is not a dead socket. The recurring cause is a reply
                        // sent to a television's ephemeral port that the television has already closed:
                        // the ICMP port-unreachable comes back and surfaces on the NEXT receive. This
                        // used to sit outside the loop's guard, so one such datagram ended discovery for
                        // the process lifetime with a single warning as the only trace.
                        consecutiveFailures++;

                        if (consecutiveFailures >= MaxConsecutiveReceiveFailures)
                        {
                            // Back off and keep listening rather than return. Returning ended discovery
                            // for the life of the process, and a brief ENETDOWN - a network blip, a
                            // switch reboot, an interface bounce - produces this many failures in
                            // milliseconds. The comment above records the same defect being fixed once
                            // already; the terminal exit was what survived it.
                            LogListenerBackingOff(exception.Message, (int)_receiveBackoff.TotalSeconds);
                            consecutiveFailures = 0;

                            await Task.Delay(_receiveBackoff, _timeProvider, stoppingToken);
                            continue;
                        }

                        LogReceiveFailed(exception.Message, consecutiveFailures);
                        continue;
                    }

                    // Deliberately NOT awaited. HandleAsync honours MX by sleeping up to five seconds
                    // before replying, and awaiting it here serialised that wait into the receive loop:
                    // the socket stopped being read while one search slept, so two televisions booting
                    // together already lost - the second reply went out past its own MX window and that
                    // renderer concluded there was no server. A device sending M-SEARCH in a loop could
                    // hold discovery down entirely. Faults are observed inside HandleSafeAsync.
                    _ = HandleSafeAsync(client, received, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        /// <summary>
        /// Runs one search to completion off the receive loop, observing whatever it throws.
        /// </summary>
        /// <remarks>
        /// Nothing awaits this, so an unobserved exception would surface far from its cause - hence the
        /// catch-all here rather than at the call site. The slot cap is what keeps "not awaited" from
        /// meaning "unbounded".
        /// </remarks>
        private async Task HandleSafeAsync(
            UdpClient client,
            UdpReceiveResult received,
            CancellationToken cancellationToken)
        {
            if (!await _replySlots.WaitAsync(TimeSpan.Zero, cancellationToken))
            {
                LogSearchDropped(received.RemoteEndPoint.ToString(), MaxConcurrentReplies);
                return;
            }

            try
            {
                await HandleAsync(client, received, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutdown while this reply was waiting out its MX delay.
            }
            catch (Exception exception)
            {
                // One malformed M-SEARCH must not end discovery for the process lifetime. The datagram
                // comes off the network, so its contents are not ours to trust.
                LogSearchFailed(received.RemoteEndPoint.ToString(), exception);
            }
            finally
            {
                _ = _replySlots.Release();
            }
        }

        public override void Dispose()
        {
            _replySlots.Dispose();
            base.Dispose();
        }

        private async Task HandleAsync(
            UdpClient client,
            UdpReceiveResult received,
            CancellationToken cancellationToken)
        {
            var message = Encoding.UTF8.GetString(received.Buffer);

            if (!message.StartsWith(SearchMethod, StringComparison.OrdinalIgnoreCase)
                || !message.Contains(DiscoverHeader, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var searchTarget = ReadHeader(message, "ST:");

            // MX is the spec's answer to amplification: a responder must wait a random interval up to MX
            // before replying, so a burst of spoofed searches spreads out instead of becoming an
            // instant 27x reply for every one. It was read by nothing, so every reply went out at once.
            var delay = ResolveSearchDelay(ReadHeader(message, "MX:"));

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, _timeProvider, cancellationToken);
            }

            if (searchTarget is null)
            {
                return;
            }

            var useLegacyMatch = _options.CurrentValue.Compatibility.UseLegacyInvertedSearchTargetMatch;
            // KNOWN LIMITATION, not an oversight to tidy away. Resolve matches its argument against this
            // server's OWN interface addresses, and what is passed here is the SEARCHER's - so it never
            // matches and always falls through to Identities[0]. On a single-NIC box that is the right
            // answer anyway; on a multi-homed one every M-SEARCH is answered with the first interface's
            // LOCATION, so a renderer can discover the server and then fail to fetch description.xml.
            //
            // Passing the local address instead is not possible from here: UdpReceiveResult does not
            // carry one. A real fix needs either SocketOptionName.PacketInformation with
            // ReceiveMessageFrom, or one listener bound per interface as the notifier already does - and
            // either reworks the discovery path, which cannot be verified without a multi-homed host and
            // a real television.
            var identity = _devices.Resolve(received.RemoteEndPoint.Address);
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var answered = 0;

            foreach (var notificationType in SsdpAdvertisement.NotificationTypesFor(identity))
            {
                if (!SsdpAdvertisement.ShouldAnswer(searchTarget, notificationType, useLegacyMatch))
                {
                    continue;
                }

                var response = SsdpMessageBuilder.CreateSearchResponse(
                    nowUtc,
                    identity.DescriptionUrl,
                    _serverSignature,
                    notificationType,
                    SsdpMessageBuilder.CreateUniqueServiceName(identity.DeviceId, notificationType));

                try
                {
                    var bytes = Encoding.UTF8.GetBytes(response);
                    _ = await client.SendAsync(bytes, received.RemoteEndPoint, cancellationToken);
                    answered++;
                }
                catch (SocketException exception)
                {
                    LogReplyFailed(received.RemoteEndPoint.ToString(), exception.Message);
                }
            }

            LogSearchAnswered(searchTarget, received.RemoteEndPoint.ToString(), answered);
        }

        /// <summary>
        /// Reads a header value out of a raw SSDP datagram.
        /// </summary>
        /// <remarks>
        /// Splits on any line ending rather than the platform's. The reference split on
        /// <c>Environment.NewLine</c>, which on Linux is a bare newline while SSDP datagrams use CRLF -
        /// so every header value there carried a trailing carriage return.
        /// </remarks>
        /// <summary>
        /// A random wait up to the requested <c>MX</c>, clamped to what the specification allows.
        /// </summary>
        /// <remarks>
        /// MX is seconds and the specification caps it at 5; anything larger or unparseable falls back to
        /// the default rather than letting a searcher choose how long this server holds a reply.
        /// </remarks>
        private static TimeSpan ResolveSearchDelay(string? mx)
        {
            if (!int.TryParse(mx, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
            {
                seconds = DefaultSearchDelaySeconds;
            }

            var ceiling = Math.Min(seconds, MaxSearchDelaySeconds);

            return TimeSpan.FromMilliseconds(Random.Shared.Next(0, ceiling * 1000));
        }

        private static string? ReadHeader(string message, string headerPrefix)
        {
            foreach (var line in message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith(headerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return line[headerPrefix.Length..].Trim();
                }
            }

            return null;
        }

        private UdpClient? TryCreateListener()
        {
            try
            {
                var client = new UdpClient(AddressFamily.InterNetwork);

                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.ExclusiveAddressUse = false;

                if (OperatingSystem.IsWindows())
                {
                    // Without this, replying to a television that has closed its ephemeral port makes the
                    // NEXT ReceiveAsync throw. Harmless on the NAS, but it breaks discovery on the dev box.
                    _ = client.Client.IOControl((IOControlCode)SioUdpConnectionReset, [0, 0, 0, 0], null);
                }
                client.Client.Bind(new IPEndPoint(IPAddress.Any, SsdpMessageBuilder.Port));
                client.JoinMulticastGroup(IPAddress.Parse(SsdpMessageBuilder.MulticastAddress));

                return client;
            }
            catch (SocketException exception)
            {
                // Port 1900 is often already held by another DLNA server or by Windows' own SSDP service.
                // Discovery is degraded but everything else still works, so this is a warning, not a stop.
                LogListenerUnavailable(exception.Message);
                return null;
            }
        }
    }
}
