using System.Net;
using System.Net.Sockets;

namespace DlnaServer.Host
{
    /// <summary>
    /// Refuses a <c>/manage</c> request that arrived from outside the local network.
    /// </summary>
    /// <remarks>
    /// The management endpoints are unauthenticated by design - a LAN appliance with no accounts - and
    /// that holds only while the LAN is the whole audience. <c>docker-compose.admin-remote.yml</c>
    /// deliberately leaves it, and its stated contract is that it exposes <b>only</b> the admin pages.
    /// It does not:
    /// <para>
    /// SSDP needs multicast, so the compose file mandates <c>network_mode: host</c> and both ports are
    /// bound with <c>ListenAnyIP</c> - the media port is already on every interface before Caddy is
    /// involved, and nothing is "published" for the file's own "never publish the media port" advice to
    /// govern. The one in-process control refusing internet-origin requests there was
    /// <c>AllowedHostsDefaults</c>' loopback-and-LAN allow-list, and <c>HostFilteringOptions</c> is
    /// process-wide rather than per-port - so widening it to the public hostname for the admin pages
    /// widened it for <c>/manage</c> too. The other two filters pass: the port is genuinely the media
    /// port, and a non-browser client sends neither <c>Sec-Fetch-Site</c> nor <c>Origin</c>.
    /// </para>
    /// <para>
    /// The hostname needed to exploit it is not even secret - the Let's Encrypt certificate the setup
    /// step obtains is published in the Certificate Transparency logs. Wherever TCP reaches the media
    /// port, one <c>curl</c> with a spoofed <c>Host</c> header reached <c>POST /manage/stop</c>,
    /// <c>/manage/recreateAllFilesInfo</c>, <c>/manage/block/72</c> and the configuration and path
    /// listings, entirely past the proxy's password.
    /// </para>
    /// <para>
    /// 404 rather than 403, matching <c>AdminSurfaceMiddleware</c>: from off the LAN this surface does
    /// not exist, and "forbidden" would confirm that it does somewhere.
    /// </para>
    /// </remarks>
    internal sealed class RejectRemoteManagementEndpointFilter : IEndpointFilter
    {
        private const string ManagementPrefix = "/manage";

        public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            var httpContext = context.HttpContext;

            if (!httpContext.Request.Path.StartsWithSegments(ManagementPrefix, StringComparison.OrdinalIgnoreCase)
                || IsLocal(httpContext.Connection.RemoteIpAddress))
            {
                return next(context);
            }

            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

            return ValueTask.FromResult<object?>(null);
        }

        /// <remarks>
        /// A null address is treated as local: it means there is no socket behind the request, which is
        /// an in-process caller rather than a remote one.
        /// </remarks>
        private static bool IsLocal(IPAddress? address)
        {
            if (address is null)
            {
                return true;
            }

            // A dual-stack socket reports an IPv4 peer as ::ffff:a.b.c.d, so the v4 rules below would
            // never match without this.
            var candidate = address.IsIPv4MappedToIPv6
                ? address.MapToIPv4()
                : address;

            if (IPAddress.IsLoopback(candidate))
            {
                return true;
            }

            return candidate.AddressFamily switch
            {
                AddressFamily.InterNetwork => IsPrivateV4(candidate.GetAddressBytes()),
                AddressFamily.InterNetworkV6 => candidate.IsIPv6LinkLocal
                    || candidate.IsIPv6SiteLocal
                    || IsUniqueLocalV6(candidate.GetAddressBytes()),
                _ => false,
            };
        }

        private static bool IsPrivateV4(byte[] octets)
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
        private static bool IsUniqueLocalV6(byte[] octets)
        {
            return (octets[0] & 0xFE) == 0xFC;
        }
    }
}
