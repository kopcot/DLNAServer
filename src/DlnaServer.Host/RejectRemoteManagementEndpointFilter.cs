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
                || LocalNetworkAddress.IsLocal(httpContext.Connection.RemoteIpAddress))
            {
                return next(context);
            }

            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

            return ValueTask.FromResult<object?>(null);
        }
    }
}
