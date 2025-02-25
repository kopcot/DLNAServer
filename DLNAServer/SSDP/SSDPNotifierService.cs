using DLNAServer.Configuration;
using DLNAServer.Types.IP.Interfaces;
using DLNAServer.Types.UPNP;
using DLNAServer.Types.UPNP.Interfaces;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DLNAServer.SSDP
{
    /// <summary>
    /// Simple Service Discovery Protocol Notification service 
    /// </summary>
    public class SSDPNotifierService : BackgroundService
    {
        private readonly ILogger<SSDPNotifierService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IPEndPoint _endpoint;
        private readonly ServerConfig _serverConfig;
        private readonly ConcurrentDictionary<(UPNPDevice device, IPEndPoint address, int ssdpPort, string notificationSubtype, string serverName), byte[]> messageDataStored = new(); 
        public SSDPNotifierService(
            ILogger<SSDPNotifierService> logger,
            IServiceScopeFactory serviceScopeFactory,
            IPEndPoint endpoint,
            ServerConfig serverConfig)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _endpoint = endpoint;
            _serverConfig = serverConfig;
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Starting SSDP notifiers for {_endpoint}...");
            messageDataStored.Clear();
            await StartNotifyingAsync(cancellationToken);
        }
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping SSDP notifiers...");
            messageDataStored.Clear();
            await StopNotifyingAsync();
            await base.StopAsync(cancellationToken);
        }
        public async Task StartNotifyingAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                using (UdpClient udpClientSender = new())
                {
                    var devices = scope.ServiceProvider.GetRequiredService<IUPNPDevices>();
                    var ip = scope.ServiceProvider.GetRequiredService<IIP>();

                    udpClientSender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udpClientSender.ExclusiveAddressUse = false;
                    udpClientSender.Client.Bind(_endpoint);
                    udpClientSender.Ttl = 10;
                    udpClientSender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 10);
                    udpClientSender.JoinMulticastGroup(ip.MulticastAddress, 10);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        foreach (var device in devices.AllUPNPDevices)
                        {
                            await SendMessage(udpClientSender, device, ip.MulticastEndPoint, ip.SSDP_PORT, "ssdp:alive"); // DLNA device discovery
                            await SendMessage(udpClientSender, device, ip.BroadcastEndPoint, ip.SSDP_PORT, "ssdp:alive"); // General announcements
                        }
                        await Task.Delay(30000, cancellationToken);
                    }

                    udpClientSender.DropMulticastGroup(ip.MulticastAddress);
                    CleanUpdClient(udpClientSender);
                };
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation($"Task was canceled");
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, $"Error in SSDPListener for IP: {_endpoint}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in SSDPListener for IP: {_endpoint}");
            }
        }

        public async Task StopNotifyingAsync()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            using (UdpClient udpClientSender = new())
            {
                var devices = scope.ServiceProvider.GetRequiredService<IUPNPDevices>();
                var ip = scope.ServiceProvider.GetRequiredService<IIP>();

                udpClientSender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClientSender.ExclusiveAddressUse = false;
                udpClientSender.Client.Bind(_endpoint);
                udpClientSender.Ttl = 10;
                udpClientSender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 10);
                //
                foreach (var device in devices.AllUPNPDevices)
                {
                    await SendMessage(udpClientSender, device, ip.MulticastEndPoint, ip.SSDP_PORT, "ssdp:byebye");
                    await SendMessage(udpClientSender, device, ip.BroadcastEndPoint, ip.SSDP_PORT, "ssdp:byebye");
                }

                CleanUpdClient(udpClientSender);

                _logger.LogDebug("SSDP stop NOTIFY message sent.");
            };
        }
        private async Task SendMessage(UdpClient udpClient, UPNPDevice device, IPEndPoint receiverEndPoint, int ssdpPort, string notificationSubtype)
        {
            try
            {
                var messageData = messageDataStored.GetOrAdd((device, receiverEndPoint, ssdpPort, notificationSubtype, _serverConfig.DlnaServerName), static (key) =>
                {
                    StringBuilder sb = new();
                    _ = sb.Append($"NOTIFY * HTTP/1.1\r\n");
                    _ = sb.Append($"HOST: {key.address.ToString()}:{key.ssdpPort}\r\n");
                    _ = sb.Append($"CACHE-CONTROL: max-age = 600\r\n");
                    _ = sb.Append($"LOCATION: {key.device.Descriptor}\r\n");
                    _ = sb.Append($"SERVER: {key.serverName}\r\n");
                    _ = sb.Append($"NTS: {key.notificationSubtype}\r\n");
                    _ = sb.Append($"NT: {key.device.Type}\r\n");
                    _ = sb.Append($"USN: {key.device.USN}\r\n");
                    _ = sb.Append("\r\n");

                    return Encoding.UTF8.GetBytes(sb.ToString());
                });

                _ = await udpClient.SendAsync(messageData, messageData.Length, receiverEndPoint);
            } 
            catch (SocketException ex)
            {
                _logger.LogError(ex, $"Error in SSDPNotifier: {ex.Message}");

                Random random = new();
                TimeSpan delay = TimeSpan.FromMinutes(_serverConfig.ServerDelayAfterUnsuccessfulSendSSDPMessageInMin).Add(TimeSpan.FromSeconds(random.Next(60)));
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in SSDPNotifier: {ex.Message}");

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
