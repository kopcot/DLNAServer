using System.Globalization;
using System.Text;

namespace DlnaServer.Upnp.Ssdp
{
    /// <summary>
    /// Builds the SSDP datagrams the server sends.
    /// </summary>
    /// <remarks>
    /// Header names, order and casing are reproduced from the reference exactly. SSDP parsers in
    /// consumer devices are frequently sloppy, and some of them do care about order.
    /// </remarks>
    public static class SsdpMessageBuilder
    {
        /// <summary>
        /// Standard SSDP multicast group.
        /// </summary>
        public const string MulticastAddress = "239.255.255.250";

        /// <summary>
        /// Standard SSDP port.
        /// </summary>
        public const int Port = 1900;

        /// <summary>
        /// <b>CACHE-CONTROL</b><br />
        /// How long a renderer may cache the advertisement, in seconds.
        /// </summary>
        public const int CacheControlMaxAgeSeconds = 600;

        /// <summary>
        /// Builds an <c>ssdp:alive</c> or <c>ssdp:byebye</c> announcement.
        /// </summary>
        /// <remarks>
        /// <c>HOST</c> carries whichever endpoint the datagram is addressed to, so a broadcast copy says
        /// <c>255.255.255.255:1900</c> and a multicast copy says <c>239.255.255.250:1900</c> - matching
        /// the reference, which sends to both.
        /// </remarks>
        public static string CreateNotify(
            string hostEndpoint,
            Uri descriptionUrl,
            string serverSignature,
            string notificationSubtype,
            string notificationType,
            string uniqueServiceName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hostEndpoint);
            ArgumentNullException.ThrowIfNull(descriptionUrl);

            var builder = new StringBuilder(512);

            _ = builder.Append("NOTIFY * HTTP/1.1\r\n");
            _ = builder.Append("HOST: ").Append(hostEndpoint).Append("\r\n");
            _ = builder.Append(CultureInfo.InvariantCulture, $"CACHE-CONTROL: max-age={CacheControlMaxAgeSeconds}\r\n");
            _ = builder.Append("LOCATION: ").Append(descriptionUrl.ToString()).Append("\r\n");
            _ = builder.Append("SERVER: ").Append(serverSignature).Append("\r\n");
            _ = builder.Append("NTS: ").Append(notificationSubtype).Append("\r\n");
            _ = builder.Append("NT: ").Append(notificationType).Append("\r\n");
            _ = builder.Append("USN: ").Append(uniqueServiceName).Append("\r\n");
            _ = builder.Append("\r\n");

            return builder.ToString();
        }

        /// <summary>
        /// Builds the unicast reply to an <c>M-SEARCH</c>.
        /// </summary>
        /// <remarks>
        /// <c>EXT:</c> is deliberately empty - the UPnP specification requires the header to be present
        /// with no value. <c>DATE</c> is RFC 1123, which is a GMT format, so it is generated from UTC;
        /// the reference formatted local time with the same pattern, which mislabels the offset.
        /// </remarks>
        public static string CreateSearchResponse(
            DateTime nowUtc,
            Uri descriptionUrl,
            string serverSignature,
            string searchTarget,
            string uniqueServiceName)
        {
            ArgumentNullException.ThrowIfNull(descriptionUrl);

            var builder = new StringBuilder(512);

            _ = builder.Append("HTTP/1.1 200 OK\r\n");
            _ = builder.Append(CultureInfo.InvariantCulture, $"CACHE-CONTROL: max-age={CacheControlMaxAgeSeconds}\r\n");
            _ = builder.Append("DATE: ").Append(nowUtc.ToString("R", CultureInfo.InvariantCulture)).Append("\r\n");
            _ = builder.Append("EXT: \r\n");
            _ = builder.Append("LOCATION: ").Append(descriptionUrl.ToString()).Append("\r\n");
            _ = builder.Append("SERVER: ").Append(serverSignature).Append("\r\n");
            _ = builder.Append("ST: ").Append(searchTarget).Append("\r\n");
            _ = builder.Append("USN: ").Append(uniqueServiceName).Append("\r\n");
            _ = builder.Append("\r\n");

            return builder.ToString();
        }

        /// <summary>
        /// Composes the <c>USN</c> for a device or service.
        /// </summary>
        /// <remarks>
        /// A bare <c>uuid:</c> target is its own USN; everything else is <c>uuid:{guid}::{type}</c>.
        /// </remarks>
        public static string CreateUniqueServiceName(Guid deviceId, string notificationType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(notificationType);

            return notificationType.StartsWith("uuid:", StringComparison.Ordinal)
                ? notificationType
                : string.Create(CultureInfo.InvariantCulture, $"uuid:{deviceId}::{notificationType}");
        }
    }
}
