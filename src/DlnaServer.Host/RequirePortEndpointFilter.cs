namespace DlnaServer.Host
{
    /// <summary>
    /// Rejects a request that arrived on a port the endpoint is not published on.
    /// The server listens on two ports and each endpoint belongs to exactly one of them.
    /// </summary>
    internal sealed class RequirePortEndpointFilter : IEndpointFilter
    {
        private readonly int _port;

        public RequirePortEndpointFilter(int port)
        {
            _port = port;
        }

        public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            return context.HttpContext.Connection.LocalPort == _port
                ? next(context)
                : ValueTask.FromResult<object?>(Results.NotFound());
        }
    }
}
