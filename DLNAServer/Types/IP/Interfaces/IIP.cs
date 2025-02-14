using System.Net;

namespace DLNAServer.Types.IP.Interfaces
{
    public interface IIP
    {
        IEnumerable<IPAddress> AllIPAddresses { get; }
        IEnumerable<IPAddress> ExternalIPAddresses { get; }
        /// <summary>
        /// SSDP uses port 1900;
        /// </summary>
        int SSDP_PORT { get; }
        /// <summary>
        /// Ip address 239.255.255.250
        /// </summary>
        IPAddress MulticastAddress { get; }
        /// <summary>
        /// Ip address 255.255.255.255
        /// </summary>
        IPAddress BroadcastAddress { get; }
        IPEndPoint MulticastEndPoint { get; }
        IPEndPoint BroadcastEndPoint { get; }
    }
}
