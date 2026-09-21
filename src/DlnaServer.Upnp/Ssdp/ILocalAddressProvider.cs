using System.Net;

namespace DlnaServer.Upnp.Ssdp
{
    /// <summary>
    /// Finds the local addresses worth advertising the server on.
    /// </summary>
    public interface ILocalAddressProvider
    {
        /// <summary>
        /// IPv4 addresses of interfaces a renderer could plausibly reach the server through.
        /// </summary>
        IReadOnlyList<IPAddress> GetBroadcastableAddresses();
    }
}
