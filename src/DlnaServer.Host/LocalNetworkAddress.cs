using System.Net;
using System.Net.Sockets;

namespace DlnaServer.Host
{
    /// <summary>
    /// Decides whether a peer address belongs to the local network rather than the open internet.
    /// </summary>
    /// <remarks>
    /// One definition for every guard that asks, so the <c>/manage</c> filter, the admin port and the SSDP
    /// listener cannot drift apart on what "local" means. Loopback, the private and link-local ranges of
    /// both families, and the carrier-grade NAT range count as local.
    /// </remarks>
    internal static class LocalNetworkAddress
    {
        // A SLAAC network's prefix is always /64, so two addresses sharing their first 8 bytes are on one link.
        private const int LinkPrefixBytes = 8;

        /// <remarks>
        /// A null address is treated as local: it means there is no socket behind the request, which is
        /// an in-process caller rather than a remote one.
        /// </remarks>
        public static bool IsLocal(IPAddress? address)
        {
            if (address is null)
            {
                return true;
            }

            // Into the stack rather than GetAddressBytes: this runs on every admin request and every datagram.
            Span<byte> bytes = stackalloc byte[16];

            if (!address.TryWriteBytes(bytes, out var length))
            {
                return false;
            }

            // A dual-stack socket reports an IPv4 peer as ::ffff:a.b.c.d, so the v4 rules below would
            // never match without this. The v4 address is its last four bytes.
            if (address.IsIPv4MappedToIPv6)
            {
                return IsPrivateV4(bytes[12..16]);
            }

            // 127.0.0.0/8, IPv4's loopback, is one of IsPrivateV4's ranges.
            return address.AddressFamily switch
            {
                AddressFamily.InterNetwork => IsPrivateV4(bytes[..length]),
                AddressFamily.InterNetworkV6 => IPAddress.IsLoopback(address)
                    || address.IsIPv6LinkLocal
                    || address.IsIPv6SiteLocal
                    || IsUniqueLocalV6(bytes),
                _ => false,
            };
        }

        /// <summary>
        /// Whether an IPv6 peer is on the same /64 as the server address it connected to.
        /// </summary>
        /// <remarks>
        /// A dual-stack LAN gives its devices global addresses as well as private ones, and a browser
        /// reaching the server by a name that resolves to its global address connects FROM its own global
        /// address - which <see cref="IsLocal"/> rightly calls public. Sharing the server's /64 is what
        /// makes it the same link: a prefix is delegated to one network, so nothing on the internet
        /// connects from inside it.
        /// </remarks>
        public static bool IsSameIPv6Link(IPAddress? remote, IPAddress? local)
        {
            if (remote is not { AddressFamily: AddressFamily.InterNetworkV6, IsIPv4MappedToIPv6: false }
                || local is not { AddressFamily: AddressFamily.InterNetworkV6, IsIPv4MappedToIPv6: false })
            {
                return false;
            }

            Span<byte> remoteBytes = stackalloc byte[16];
            Span<byte> localBytes = stackalloc byte[16];

            return remote.TryWriteBytes(remoteBytes, out _)
                && local.TryWriteBytes(localBytes, out _)
                && remoteBytes[..LinkPrefixBytes].SequenceEqual(localBytes[..LinkPrefixBytes]);
        }

        private static bool IsPrivateV4(ReadOnlySpan<byte> octets)
        {
            return octets[0] switch
            {
                10 => true,
                127 => true,
                169 => octets[1] == 254,
                172 => octets[1] >= 16 && octets[1] <= 31,
                192 => octets[1] == 168,

                // 100.64.0.0/10, the carrier-grade NAT range. Tailscale and similar overlays hand out
                // addresses from it, and reaching the server over one of those is a deliberate private
                // link rather than the open internet.
                100 => octets[1] >= 64 && octets[1] <= 127,
                _ => false,
            };
        }

        /// <remarks>
        /// <c>fc00::/7</c>, the IPv6 unique-local range. <see cref="IPAddress.IsIPv6SiteLocal"/> covers
        /// only the deprecated <c>fec0::/10</c>, so it does not answer this on its own.
        /// </remarks>
        private static bool IsUniqueLocalV6(ReadOnlySpan<byte> octets)
        {
            return (octets[0] & 0xFE) == 0xFC;
        }
    }
}
