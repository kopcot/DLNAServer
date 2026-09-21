namespace DlnaServer.Host
{
    /// <summary>
    /// Requires the admin port for <c>/admin</c> and the media port for everything else.
    /// </summary>
    /// <remarks>
    /// <see cref="RequirePortEndpointFilter"/> binds one endpoint to one port, which is right when the
    /// endpoints are mapped individually. Controllers are mapped as a group, though, and that group now
    /// straddles both ports: <see cref="Controllers.AdminMediaController"/> serves the admin UI's
    /// previews while every other controller serves renderers. One filter over the group therefore has
    /// to decide per request, and the path prefix is what already distinguishes them - the admin surface
    /// is <c>/admin/*</c> by convention, including <c>/admin/health</c> and the Blazor pages.
    /// <para>
    /// Getting this wrong is not obvious from a browser: a controller bound to the wrong port answers 404
    /// exactly as a missing file does.
    /// </para>
    /// </remarks>
    internal sealed class AdminOrMediaPortEndpointFilter : IEndpointFilter
    {
        private const string AdminPrefix = "/admin";

        private readonly int _mediaPort;
        private readonly int _adminPort;

        public AdminOrMediaPortEndpointFilter(int mediaPort, int adminPort)
        {
            _mediaPort = mediaPort;
            _adminPort = adminPort;
        }

        public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            var isAdminPath = context.HttpContext.Request.Path
                .StartsWithSegments(AdminPrefix, StringComparison.OrdinalIgnoreCase);

            var required = isAdminPath ? _adminPort : _mediaPort;

            return context.HttpContext.Connection.LocalPort == required
                ? next(context)
                : ValueTask.FromResult<object?>(Results.NotFound());
        }
    }
}
