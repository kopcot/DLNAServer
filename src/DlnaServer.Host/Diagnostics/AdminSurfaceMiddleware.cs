using System.Net;
using DlnaServer.Upnp.Constants;

namespace DlnaServer.Host.Diagnostics
{
    /// <summary>
    /// Keeps the admin surface on the admin port, whatever serves it.
    /// </summary>
    /// <remarks>
    /// <see cref="RequirePortEndpointFilter"/> cannot do this job for the admin UI. Endpoint filters run
    /// in the routing pipeline used by controllers and minimal APIs, and Razor component endpoints are
    /// not dispatched through it - applying the filter to <c>MapRazorComponents</c> compiles, reads as
    /// though it works, and was measured doing nothing: every admin page still answered 200 on the media
    /// port. Middleware runs for every request regardless of who ends up handling it, so this is the only
    /// place the guarantee can actually be made.
    /// <para>
    /// The Blazor framework paths are included: nothing on the media port needs the component script, the
    /// circuit or the class library's stylesheet, and leaving them reachable would advertise that an admin
    /// interface exists on a port whose only audience is televisions.
    /// </para>
    /// <para>
    /// The admin port's root redirects to <see cref="AdminRoot"/> rather than answering 404. Confinement
    /// keys on the path as well as the port, so a bare <c>/</c> was classified as media content and
    /// refused - which behind a password prompt reads as a broken deployment rather than a wrong URL. The
    /// media port's <c>/</c> is untouched: it serves <c>description.xml</c>, which some renderers fetch
    /// instead of the LOCATION that SSDP advertises.
    /// </para>
    /// <para>
    /// The admin port also answers only the local network, with the same 404 as the <c>/manage</c>
    /// filter. Kestrel binds it with <c>ListenAnyIP</c>, which includes IPv6, and a NAS with a global IPv6
    /// address is reachable from the internet wherever the router lets IPv6 in - no NAT stands in the
    /// way. This is not authentication, which standing decision 1 rules out; it keeps the LAN the whole
    /// audience that decision assumes. The source checked is the SOCKET's peer, recorded by
    /// <see cref="RecordConnectionPeer"/> before <c>UseForwardedHeaders</c> rewrites
    /// <see cref="ConnectionInfo.RemoteIpAddress"/>. Behind the remote-admin proxy that rewrite names the
    /// internet client the proxy has already put through its password, while the socket is the proxy on
    /// loopback - so the proxy keeps working and a direct caller cannot pass as one. The
    /// <c>X-Original-For</c> header would not do: a caller the forwarder does not trust keeps whatever
    /// value it sent.
    /// </para>
    /// </remarks>
    internal sealed class AdminSurfaceMiddleware
    {
        private const string AdminRoot = "/admin";
        private const string RootPath = "/";

        private static readonly object _connectionPeerKey = new();

        /// <summary>
        /// Path prefixes that belong to the admin port alone.
        /// </summary>
        private static readonly string[] _adminPrefixes =
        [
            AdminRoot,
            "/_blazor",
            "/_framework",
            "/_content",
        ];

        /// <summary>
        /// Paths that belong to the media port alone.
        /// </summary>
        /// <remarks>
        /// The four SOAP control endpoints are registered through SoapCore's <c>UseSoapEndpoint</c>, which
        /// is old-style middleware on <c>IApplicationBuilder</c> rather than endpoint routing - so
        /// <c>AddEndpointFilter</c> structurally cannot reach them and they answered on the admin port as
        /// well. Nothing was reachable there that a renderer could not already reach on its own port, but
        /// it broke the one-surface-per-port model in the direction nothing was guarding, and left the
        /// whole ContentDirectory API on the port an operator browses.
        /// <para>
        /// The constants themselves, not the same four strings typed again. They were character-identical
        /// copies of <see cref="UpnpServices.ControlPath"/>, which <c>Program.cs</c> maps the endpoints
        /// from - so renaming one there would have moved the endpoint and left this guard watching the
        /// old path, quietly putting the whole ContentDirectory API back on the admin port. That is the
        /// exact failure this list exists to prevent, and the duplication was the one way left to cause
        /// it. <c>AdminSurfaceMiddlewareTest</c> pins the two together as well.
        /// </para>
        /// </remarks>
        internal static readonly string[] MediaOnlyPaths =
        [
            UpnpServices.ControlPath.ContentDirectory,
            UpnpServices.ControlPath.ConnectionManager,
            UpnpServices.ControlPath.AvTransport,
            UpnpServices.ControlPath.MediaReceiverRegistrar,
        ];

        private readonly RequestDelegate _next;
        private readonly int _adminPort;

        public AdminSurfaceMiddleware(RequestDelegate next, int adminPort)
        {
            _next = next;
            _adminPort = adminPort;
        }

        public Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var path = context.Request.Path;
            var isAdminPort = context.Connection.LocalPort == _adminPort;

            if (isAdminPort && !IsFromLocalNetwork(context))
            {
                // The same answer the /manage filter gives: from off the LAN this surface does not exist.
                context.Response.StatusCode = StatusCodes.Status404NotFound;

                return Task.CompletedTask;
            }

            if (isAdminPort && path.Equals(RootPath, StringComparison.Ordinal))
            {
                // Found rather than Moved Permanently: a browser keeps a permanent redirect until its
                // cache is cleared, so this could not be taken back if the root ever serves its own page.
                context.Response.Redirect(AdminRoot);

                return Task.CompletedTask;
            }

            if (IsAdminSurface(path))
            {
                if (isAdminPort)
                {
                    return _next(context);
                }
            }
            else if (!IsMediaOnly(path) || !isAdminPort)
            {
                return _next(context);
            }

            // 404 rather than 403: on the media port this surface does not exist, and saying "forbidden"
            // would confirm that it does somewhere.
            context.Response.StatusCode = StatusCodes.Status404NotFound;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Keeps the socket's own peer for an admin-port request, before anything rewrites it.
        /// </summary>
        /// <remarks>
        /// Must run ahead of <c>UseForwardedHeaders</c>, which replaces
        /// <see cref="ConnectionInfo.RemoteIpAddress"/> with the client a trusted proxy names. Without a
        /// record the check falls back to that rewritten address, which behind the proxy is an internet
        /// client - so a missing call refuses the proxy's requests rather than admitting a direct caller.
        /// Only admin-port requests are recorded, so the media port's requests do not each pay for an
        /// items dictionary.
        /// </remarks>
        internal static void RecordConnectionPeer(HttpContext context, int adminPort)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Connection.LocalPort == adminPort)
            {
                context.Items[_connectionPeerKey] = context.Connection.RemoteIpAddress;
            }
        }

        private static bool IsFromLocalNetwork(HttpContext context)
        {
            var peer = context.Items.TryGetValue(_connectionPeerKey, out var recorded)
                ? recorded as IPAddress
                : context.Connection.RemoteIpAddress;

            return LocalNetworkAddress.IsLocal(peer)
                || LocalNetworkAddress.IsSameIPv6Link(peer, context.Connection.LocalIpAddress);
        }

        private static bool IsMediaOnly(PathString path)
        {
            foreach (var mediaPath in MediaOnlyPaths)
            {
                if (path.Equals(mediaPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAdminSurface(PathString path)
        {
            foreach (var prefix in _adminPrefixes)
            {
                if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
