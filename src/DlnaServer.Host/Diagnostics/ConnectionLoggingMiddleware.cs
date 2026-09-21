namespace DlnaServer.Host.Diagnostics
{
    /// <summary>
    /// Logs who connected, what they asked for, and what they got.
    /// </summary>
    /// <remarks>
    /// The reference calls a connection-logging helper by hand at the top of every controller action and
    /// SOAP operation. One middleware covers the same ground without the duplication, and it also covers
    /// the SOAP endpoints and anything added later, which a per-action call only does if somebody
    /// remembers.
    /// <para>
    /// <c>User-Agent</c> is the field the reference does not record and the one that matters most when
    /// two devices behave differently: it names the renderer, so a log can be read as "the television did
    /// this, VLC did that" rather than guessed at from request shapes.
    /// </para>
    /// <para>
    /// Debug level, so this is silent until <c>Dlna.Server.DebugMode</c> is turned on. A renderer
    /// streaming a film issues a request per byte range, and that volume is only wanted while a problem
    /// is being chased.
    /// </para>
    /// </remarks>
    internal sealed partial class ConnectionLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ConnectionLoggingMiddleware> _logger;

        public ConnectionLoggingMiddleware(RequestDelegate next, ILogger<ConnectionLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // Nothing is formatted unless Debug is enabled, so the check keeps the cost off the hot path.
            if (!_logger.IsEnabled(LogLevel.Debug))
            {
                await _next(context);
                return;
            }

            var connection = context.Connection;
            var request = context.Request;

            LogRequestReceived(
                request.Method,
                request.Path.Value ?? "/",
                request.QueryString.HasValue ? request.QueryString.Value! : string.Empty,
                connection.RemoteIpAddress?.ToString(),
                connection.RemotePort,
                connection.LocalIpAddress?.ToString(),
                connection.LocalPort,
                request.Headers.UserAgent.ToString(),
                request.Headers.Range.ToString());

            await _next(context);

            LogResponseSent(
                request.Method,
                request.Path.Value ?? "/",
                context.Response.StatusCode,
                context.Response.ContentLength);
        }
    }
}
