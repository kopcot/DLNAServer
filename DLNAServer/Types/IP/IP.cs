using DLNAServer.Types.IP.Interfaces;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DLNAServer.Types.IP
{
    public class IP : IIP
    {
        public readonly ILogger _logger;
        public IP(ILogger<IP> logger)
        {
            _logger = logger;
        }
        public IEnumerable<IPAddress> AllIPAddresses => _allIPAddresses.Value;

        public IEnumerable<IPAddress> ExternalIPAddresses => _externalIPAddresses.Value;

        private readonly Lazy<IPAddress[]> _allIPAddresses = new(() =>
        {
            try
            {
                return GetIPsDefault();
            }
            catch
            {
                return GetIPsFallback();
            }
        });
        private readonly Lazy<IPAddress[]> _externalIPAddresses = new(() =>
        {
            try
            {
                return GetIPsDefault().Where(static (ip) => !IPAddress.IsLoopback(ip)).ToArray();
            }
            catch
            {
                return GetIPsFallback().Where(static (ip) => !IPAddress.IsLoopback(ip)).ToArray();
            }
        });

        private static IPAddress[] GetIPsDefault()
        {
            var addresses = NetworkInterface
                .GetAllNetworkInterfaces()
                .Select(static (ni) => ni.GetIPProperties())
                .Where(static (ipProperties) => ipProperties
                    .GatewayAddresses
                    .Any(static (ga) => !ga.Address.Equals(IPAddress.Any)))
                .SelectMany(static (ipProperties) => ipProperties
                    .UnicastAddresses
                    .Where(static (uniInfo) => uniInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(static (uniInfo) => uniInfo.Address))
                .ToArray();

            return addresses.Length > 0 ? addresses : throw new ApplicationException("No IP address found.");
        }
        private static IPAddress[] GetIPsFallback()
        {
            var addresses = Dns
                .GetHostEntry(Dns.GetHostName())
                .AddressList
                .Where(static (ip) => ip.AddressFamily == AddressFamily.InterNetwork)
                .ToArray();

            return addresses.Length > 0 ? addresses : throw new ApplicationException("No IP");
        }

        private const int ssdp_PORT = 1900;
        private static readonly Lazy<IPAddress> _multicastAddress = new(static () => IPAddress.Parse("239.255.255.250"));
        private static readonly Lazy<IPAddress> _broadcastAddress = new(static () => IPAddress.Parse("255.255.255.255"));
        private static readonly Lazy<IPEndPoint> _multicastEndPoint = new(static () => new(_multicastAddress.Value, ssdp_PORT));
        private static readonly Lazy<IPEndPoint> _broadcastEndPoint = new(static () => new(_broadcastAddress.Value, ssdp_PORT));
        public IPAddress MulticastAddress => _multicastAddress.Value;
        public IPAddress BroadcastAddress => _broadcastAddress.Value;
        public IPEndPoint MulticastEndPoint => _multicastEndPoint.Value;
        public IPEndPoint BroadcastEndPoint => _broadcastEndPoint.Value;
        public int SSDP_PORT => ssdp_PORT;
    }
}
