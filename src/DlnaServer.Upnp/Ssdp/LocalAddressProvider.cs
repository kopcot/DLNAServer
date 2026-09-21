using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DlnaServer.Upnp.Ssdp
{
    /// <inheritdoc cref="ILocalAddressProvider"/>
    public sealed class LocalAddressProvider : ILocalAddressProvider
    {
        public IReadOnlyList<IPAddress> GetBroadcastableAddresses()
        {
            try
            {
                var addresses = Enumerate().ToArray();

                return addresses.Length > 0 ? addresses : Fallback();
            }
            catch (NetworkInformationException)
            {
                return Fallback();
            }
        }

        /// <summary>
        /// IPv4 addresses on interfaces that are up and have a real default gateway.
        /// </summary>
        /// <remarks>
        /// The gateway test is how the reference distinguished a real LAN interface from the many virtual
        /// ones a machine accumulates - Hyper-V switches, WSL, VPN tunnels - which would otherwise each
        /// get their own SSDP announcement loop advertising an address no renderer can reach.
        /// </remarks>
        private static IEnumerable<IPAddress> Enumerate()
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                var properties = adapter.GetIPProperties();

                var hasGateway = properties.GatewayAddresses
                    .Any(static gateway => gateway.Address is not null
                        && !gateway.Address.Equals(IPAddress.Any)
                        && gateway.Address.AddressFamily == AddressFamily.InterNetwork);

                if (!hasGateway)
                {
                    continue;
                }

                foreach (var unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(unicast.Address))
                    {
                        yield return unicast.Address;
                    }
                }
            }
        }

        /// <summary>
        /// Used when interface enumeration finds nothing usable, so the server still advertises somewhere.
        /// </summary>
        private static IPAddress[] Fallback()
        {
            try
            {
                return Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .Where(static address => address.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(address))
                    .ToArray();
            }
            catch (SocketException)
            {
                return [];
            }
        }
    }
}
