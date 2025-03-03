using DLNAServer.Common;
using DLNAServer.Configuration;
using DLNAServer.Helpers.Logger;
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
    public partial class SSDPListenerService : BackgroundService
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
        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            InformationStarting();
            _udpClientSenders.Clear();
            return StartListeningAsync(cancellationToken);
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            InformationStopping();
            _udpClientSenders.Clear();
            return base.StopAsync(cancellationToken);
        }

        public async Task StartListeningAsync(CancellationToken cancellationToken)
        {
            try
            {
                DebugStartedListening();

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
                    const string headerMSearch = "M-SEARCH";
                    const string headerMan = "MAN: \"ssdp:discover\"";

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        result = await udpClientReceiver.ReceiveAsync(cancellationToken);
                        receivedMessage = decoder.GetString(result.Buffer);

                        // Check if it's an M-SEARCH request
                        if (receivedMessage.Contains(headerMSearch, StringComparison.OrdinalIgnoreCase)
                            && receivedMessage.Contains(headerMan, StringComparison.OrdinalIgnoreCase))
                        {
                            await HandleSearchRequestAsync(receivedMessage, result.RemoteEndPoint, upnpDevices.AllUPNPDevices);
                        }
                    }

                    DebugListeningCancelationRequest();

                    udpClientReceiver.DropMulticastGroup(ip.MulticastAddress);
                    CleanUpdClient(udpClientReceiver);
                    foreach (var udpClientSender in _udpClientSenders.Values)
                    {
                        CleanUpdClient(udpClientSender);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                LoggerHelper.LogWarningTaskCanceled(_logger);
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.LogWarningOperationCanceled(_logger);
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
            }

            _logger.LogGeneralWarningMessage($"{nameof(StartListeningAsync)} - finished");
        }

        private async Task HandleSearchRequestAsync(string message, IPEndPoint remoteEndPoint, UPNPDevice[] upnpDevices)
        {
            var headers = message.Split(Environment.NewLine);
            var searchTarget = headers.FirstOrDefault(static (h) => h.StartsWith("ST:"));
            var devicesEndpoint = upnpDevices.GroupBy(static (d) => d.Endpoint);
            foreach (var devices in devicesEndpoint.ToList())
            {
                bool isMessageSend = true;

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
                        isMessageSend &= await SendMessage(udpClient, device, remoteEndPoint);
                    }

                    if (!isMessageSend)
                    {
                        CleanUpdClient(udpClient);
                        _ = _udpClientSenders.Remove(devices.Key);

                        _logger.LogGeneralWarningMessage($"{nameof(HandleSearchRequestAsync)} - message not send");
                        break;
                    }
                }
            }
        }

        private async Task<bool> SendMessage(UdpClient udpClient, UPNPDevice device, IPEndPoint remoteEndPoint)
        {
            try
            {
                StringBuilder sb = new();
                _ = sb.Append("HTTP/1.1 200 OK\r\n");
                _ = sb.Append("CACHE-CONTROL: max-age=600\r\n");
                _ = sb.Append("DATE: ").Append(DateTime.Now.ToString("R")).Append("\r\n");
                _ = sb.Append("EXT: ").Append("\r\n");
                _ = sb.Append("LOCATION: ").Append(device.Descriptor).Append("\r\n");
                _ = sb.Append("SERVER: ").Append(_serverConfig.DlnaServerSignature).Append("\r\n");
                _ = sb.Append("ST: ").Append(device.Type).Append("\r\n");
                _ = sb.Append("USN: ").Append(device.USN).Append("\r\n");
                _ = sb.Append("\r\n");

                // Convert the response to bytes
                byte[] responseBytes = GC.AllocateUninitializedArray<byte>(decoder.GetByteCount(sb.ToString()), pinned: false);
                _ = decoder.GetBytes(sb.ToString(), 0, sb.Length, responseBytes, 0);

                // Send SSDP response
                _ = await udpClient.SendAsync(responseBytes, responseBytes.Length, remoteEndPoint);

                DebugSendResponse(remoteEndPoint.Address, remoteEndPoint.Port, device.Descriptor);

                _ = sb.Clear();

                return true;
            }
            catch (SocketException ex)
            {
                Random random = new();
                TimeSpan delay = TimeSpan.FromMinutes(_serverConfig.ServerDelayAfterUnsuccessfulSendSSDPMessageInMin).Add(TimeSpan.FromSeconds(random.Next(60)));

                ErrorStopNotifySend(ex.Message, delay.TotalMinutes);

                await Task.Delay(delay);

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);

                Random random = new();
                TimeSpan delay = TimeSpanValues.Time1min.Add(TimeSpan.FromSeconds(random.Next(180)));
                await Task.Delay(delay);

                return false;
            }
        }
        private static void CleanUpdClient(in UdpClient udpClient)
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
