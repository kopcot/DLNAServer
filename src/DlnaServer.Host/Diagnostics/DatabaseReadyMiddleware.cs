using DlnaServer.Core.Hosting;

namespace DlnaServer.Host.Diagnostics
{
    /// <summary>
    /// Answers 503 while the database still has no usable schema, instead of letting a request reach it.
    /// </summary>
    /// <remarks>
    /// <see cref="IDatabaseReadySignal"/> already sequences the background services, and that is where it
    /// was needed first - but nothing held the HTTP pipeline back. Kestrel is listening long before
    /// migrations finish, so on a fresh deployment, and again immediately after <i>Recreate database</i>,
    /// a television's Browse or the dashboard's first query hit a schema that did not exist yet and got a
    /// bare 500 with no indication that waiting would fix it.
    /// <para>
    /// A short timeout rather than an unbounded wait: a renderer that blocks forever on its first request
    /// is worse than one told to come back, and 503 with <c>Retry-After</c> is the answer both a browser
    /// and a UPnP control point already understand.
    /// </para>
    /// </remarks>
    internal sealed class DatabaseReadyMiddleware
    {
        private static readonly TimeSpan _waitLimit = TimeSpan.FromSeconds(5);

        private const string RetryAfterSeconds = "5";

        private readonly RequestDelegate _next;
        private readonly IDatabaseReadySignal _readySignal;

        public DatabaseReadyMiddleware(RequestDelegate next, IDatabaseReadySignal readySignal)
        {
            ArgumentNullException.ThrowIfNull(next);
            ArgumentNullException.ThrowIfNull(readySignal);

            _next = next;
            _readySignal = readySignal;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(_waitLimit);

            try
            {
                await _readySignal.WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.Headers.RetryAfter = RetryAfterSeconds;

                return;
            }

            await _next(context);
        }
    }
}
