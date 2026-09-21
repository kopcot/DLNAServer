using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DlnaServer.Upnp.Ssdp
{
    /// <summary>
    /// The identity this server advertises on one network interface.
    /// </summary>
    /// <param name="DeviceId">UUID used as the device's <c>UDN</c> and in every <c>USN</c>.</param>
    /// <param name="Address">Local address the identity belongs to.</param>
    /// <param name="Port">Port the media and UPnP endpoints listen on.</param>
    public sealed record UpnpDeviceIdentity(Guid DeviceId, IPAddress Address, int Port)
    {
        /// <summary>
        /// URL a renderer fetches to read <c>description.xml</c> for this identity.
        /// </summary>
        public Uri DescriptionUrl { get; } =
            new($"http://{Address}:{Port}/media/description.xml?uuid={DeviceId}");

        /// <summary>
        /// Derives a stable device identifier for a machine and address.
        /// </summary>
        /// <remarks>
        /// Deterministic rather than random. The reference called <c>Guid.NewGuid()</c> at every start,
        /// so each restart looked like a brand-new device: renderers kept the old entry until its
        /// <c>max-age</c> lapsed and showed the server twice in the meantime. Deriving it from the machine
        /// name and address keeps the identity stable across restarts without storing anything, while
        /// still differing between machines and between interfaces.
        /// </remarks>
        public static Guid CreateStableDeviceId(string machineName, IPAddress address, int port)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(machineName);
            ArgumentNullException.ThrowIfNull(address);

            var seed = string.Concat(machineName, "|", address.ToString(), "|", port);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));

            return new Guid(hash.AsSpan(0, 16));
        }
    }
}
