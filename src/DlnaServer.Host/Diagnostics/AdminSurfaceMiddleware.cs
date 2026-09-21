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
    /// </remarks>
    internal sealed class AdminSurfaceMiddleware
    {
        private const string AdminRoot = "/admin";
        private const string RootPath = "/";

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
