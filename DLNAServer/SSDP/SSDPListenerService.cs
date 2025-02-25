using DLNAServer.Configuration;
using DLNAServer.Types.IP.Interfaces;
using DLNAServer.Types.UPNP;
using DLNAServer.Types.UPNP.Interfaces;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DLNAServer.SSDP
{
    /// <summary>
    /// Simple Service Discovery Protocol Listener service 
    /// </summary>
    public class SSDPListenerService : BackgroundService
    {
        private readonly ILogger<SSDPListenerService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ServerConfig _serverConfig;
        private readonly Dictionary<IPEndPoint, UdpClient> _udpClientSenders = [];
        private readonly Encoding decoder = Encoding.UTF8;
        public SSDPListenerService(
            ILogger<SSDPListenerService> logger,
            IServiceScopeFactory serviceScopeFactory,
            ServerConfig serverConfig)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _serverConfig = serverConfig;
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting SSDP listeners...");
            _udpClientSenders.Clear();
            await StartListeningAsync(cancellationToken);
        }
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping SSDP listeners...");
            _udpClientSenders.Clear();
            await base.StopAsync(cancellationToken);
        }

        public async Task StartListeningAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Listening for M-SEARCH requests...");

                using (var scope = _serviceScopeFactory.CreateScope())
                using (var udpClientReceiver = new UdpClient())
                {
                    var upnpDevices = scope.ServiceProvider.GetRequiredService<IUPNPDevices>();
                    var ip = scope.ServiceProvider.GetRequiredService<IIP>();

                    IPEndPoint localEndPoint = new(IPAddress.Any, ip.SSDP_PORT);

                    // Join the multicast group to listen for M-SEARCH requests
                    udpClientReceiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udpClientReceiver.ExclusiveAddressUse = false;
                    udpClientReceiver.Client.Bind(localEndPoint);
                    udpClientReceiver.Ttl = 10;
                    udpClientReceiver.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 10);
                    udpClientReceiver.JoinMulticastGroup(ip.MulticastAddress, 10);

                    UdpReceiveResult result;
                    string? receivedMessage;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        result = await udpClientReceiver.ReceiveAsync(cancellationToken);
                        receivedMessage = decoder.GetString(result.Buffer);

                        // Check if it's an M-SEARCH request
                        if (receivedMessage.Contains("M-SEARCH", StringComparison.OrdinalIgnoreCase)
                            && receivedMessage.Contains("MAN: \"ssdp:discover\"", StringComparison.OrdinalIgnoreCase))
                        {
                            await HandleSearchRequestAsync(receivedMessage, result.RemoteEndPoint, upnpDevices.AllUPNPDevices);
                        }
                    }

                    _logger.LogDebug("Listening for M-SEARCH requests... (Cancellation requested)");

                    udpClientReceiver.DropMulticastGroup(ip.MulticastAddress);
                    CleanUpdClient(udpClientReceiver);
                    foreach (var udpClientSender in _udpClientSenders.Values)
                    {
                        CleanUpdClient(udpClientSender);
                    }
                };
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation($"Task was canceled");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in SSDPListener: {ex.Message}");
            }
        }

        private async Task HandleSearchRequestAsync(string message, IPEndPoint remoteEndPoint, UPNPDevice[] upnpDevices)
        {
            var headers = message.Split(Environment.NewLine);
            var searchTarget = headers.FirstOrDefault(static (h) => h.StartsWith("ST:"));
            var devicesEndpoint = upnpDevices.GroupBy(static (d) => d.Endpoint);
            foreach (var devices in devicesEndpoint.ToList())
            {
                if (!_udpClientSenders.TryGetValue(devices.Key, out var udpClient))
                {
                    udpClient = new UdpClient();
                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udpClient.ExclusiveAddressUse = false;
                    udpClient.Client.Bind(devices.Key);
                    udpClient.Ttl = 10;
                    udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 10);

                    _udpClientSenders.Add(devices.Key, udpClient);
                }
                foreach (var device in devices.ToList())
                {
                    // Check the "ST" (Search Target) header to determine what the client is looking for
                    if (searchTarget != null &&
                        !searchTarget.Contains(device.Type, StringComparison.CurrentCultureIgnoreCase))
                    {
                        await SendMessage(udpClient, device, remoteEndPoint);
                    }
                }
            }
        }

        private async Task SendMessage(UdpClient udpClient, UPNPDevice device, IPEndPoint remoteEndPoint)
        {
            try
            {
                StringBuilder sb = new();
                _ = sb.Append($"HTTP/1.1 200 OK\r\n");
                _ = sb.Append($"CACHE-CONTROL: max-age=600\r\n");
                _ = sb.Append($"DATE: {DateTime.Now:R}\r\n");
                _ = sb.Append($"EXT: {string.Empty}\r\n");
                _ = sb.Append($"LOCATION: {device.Descriptor}\r\n");
                _ = sb.Append($"SERVER: {_serverConfig.DlnaServerName}\r\n");
                _ = sb.Append($"ST: {device.Type}\r\n");
                _ = sb.Append($"USN: {device.USN}\r\n");
                _ = sb.Append($"\r\n");

                // Convert the response to bytes
                byte[] responseBytes = GC.AllocateUninitializedArray<byte>(decoder.GetByteCount(sb.ToString()), pinned: false);
                _ = decoder.GetBytes(sb.ToString(), 0, sb.Length, responseBytes, 0);

                // Send SSDP response
                _ = await udpClient.SendAsync(responseBytes, responseBytes.Length, remoteEndPoint);

                _logger.LogDebug($"Sent SSDP response to {remoteEndPoint.Address}:{remoteEndPoint.Port}; {device.Descriptor}");

                _ = sb.Clear();
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, $"Error in SSDPListener: {ex.Message}");

                Random random = new();
                TimeSpan delay = TimeSpan.FromMinutes(_serverConfig.ServerDelayAfterUnsuccessfulSendSSDPMessageInMin).Add(TimeSpan.FromSeconds(random.Next(60)));
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in SSDPListener: {ex.Message}");

                Random random = new();
                TimeSpan delay = TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(random.Next(180)));
                await Task.Delay(delay);
            }
        }
        private static void CleanUpdClient(UdpClient udpClient)
        {
            if (udpClient.Client.Connected)
            {
                udpClient.Client.Shutdown(SocketShutdown.Both);
            }

            udpClient.Client.Close();
            udpClient.Close();
            udpClient.Dispose();
        }
    }
}
