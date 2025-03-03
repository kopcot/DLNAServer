using DLNAServer.Common;
using DLNAServer.Configuration;
using DLNAServer.Helpers.Logger;
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
    public partial class SSDPNotifierService : BackgroundService
    {
        private readonly ILogger<SSDPNotifierService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IPEndPoint _endpoint;
        private readonly ServerConfig _serverConfig;
        private readonly ConcurrentDictionary<(UPNPDevice device, IPEndPoint address, int ssdpPort, string notificationSubtype, string serverSignature), byte[]> messageDataStored = new();
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
        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            InformationStarting(_endpoint);
            messageDataStored.Clear();
            return StartNotifyingAsync(cancellationToken);
        }
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            InformationStopping();
            messageDataStored.Clear();
            await StopNotifyingAsync();
            await base.StopAsync(cancellationToken);
        }
        public async Task StartNotifyingAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    bool isMessageSend = true;
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

                        const string notification = "ssdp:alive";

                        while (!cancellationToken.IsCancellationRequested)
                        {
                            foreach (var device in devices.AllUPNPDevices)
                            {
                                isMessageSend &= await SendMessage(udpClientSender, device, ip.MulticastEndPoint, ip.SSDP_PORT, notification); // DLNA device discovery
                                isMessageSend &= await SendMessage(udpClientSender, device, ip.BroadcastEndPoint, ip.SSDP_PORT, notification); // General announcements 
                            }
                            await Task.Delay(30000, cancellationToken);

                            if (!isMessageSend)
                            {
                                _logger.LogGeneralWarningMessage($"{nameof(StartNotifyingAsync)} - message not send");
                                break;
                            }
                        }

                        udpClientSender.DropMulticastGroup(ip.MulticastAddress);
                        CleanUpdClient(udpClientSender);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                LoggerHelper.LogWarningTaskCanceled(_logger);
            }
            catch (SocketException ex)
            {
                _logger.LogGeneralErrorMessage(ex);
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
            }

            _logger.LogGeneralWarningMessage($"{nameof(StartNotifyingAsync)} - finished");
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
                    _ = await SendMessage(udpClientSender, device, ip.MulticastEndPoint, ip.SSDP_PORT, "ssdp:byebye");
                    _ = await SendMessage(udpClientSender, device, ip.BroadcastEndPoint, ip.SSDP_PORT, "ssdp:byebye");
                }

                CleanUpdClient(udpClientSender);

                DebugStopNotifySend();
            }
        }
        private async Task<bool> SendMessage(UdpClient udpClient, UPNPDevice device, IPEndPoint receiverEndPoint, int ssdpPort, string notificationSubtype)
        {
            try
            {
                var messageData = messageDataStored.GetOrAdd((device, receiverEndPoint, ssdpPort, notificationSubtype, _serverConfig.DlnaServerSignature), static (key) =>
                {
                    StringBuilder sb = new();
                    _ = sb.Append("NOTIFY * HTTP/1.1\r\n");
                    _ = sb.Append("HOST: ").Append(key.address.ToString()).Append("\r\n");
                    _ = sb.Append("CACHE-CONTROL: max-age=600\r\n");
                    _ = sb.Append("LOCATION: ").Append(key.device.Descriptor).Append("\r\n");
                    _ = sb.Append("SERVER: ").Append(key.serverSignature).Append("\r\n");
                    _ = sb.Append("NTS: ").Append(key.notificationSubtype).Append("\r\n");
                    _ = sb.Append("NT: ").Append(key.device.Type).Append("\r\n");
                    _ = sb.Append("USN: ").Append(key.device.USN).Append("\r\n");
                    _ = sb.Append("\r\n");

                    return Encoding.UTF8.GetBytes(sb.ToString());
                });

                _ = await udpClient.SendAsync(messageData, messageData.Length, receiverEndPoint);

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
