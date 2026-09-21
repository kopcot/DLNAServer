using System.Globalization;
using DlnaServer.Core.Diagnostics;

namespace DlnaServer.Host.Diagnostics
{
    /// <summary>
    /// Answers renderer requests with <c>503 Service Unavailable</c> while a block is in force.
    /// </summary>
    /// <remarks>
    /// <c>/manage</c> and the admin surface are exempt, deliberately: blocking either would make a block
    /// impossible to lift without restarting the process. The admin half was missing, so a block took the
    /// Unblock button down with everything else - the page 503'd on the next reload and recovery meant a
    /// shell on the NAS. 503 rather than an error is what tells a renderer to come back rather than to
    /// treat the server as gone, and <c>Retry-After</c> says when.
    /// </remarks>
    internal sealed partial class ApiBlockingMiddleware
    {
        /// <remarks>
        /// The Blazor framework paths are included because the admin pages are useless without them: the
        /// component script, the circuit negotiation and the class library's stylesheet all sit under
        /// these prefixes, so 503ing them leaves a blank page with a reconnect banner.
        /// </remarks>
        private static readonly string[] _exemptPrefixes =
        [
            "/manage",
            "/admin",
            "/_blazor",
            "/_framework",
            "/_content",
        ];

        private readonly RequestDelegate _next;
        private readonly IApiBlocker _blocker;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ApiBlockingMiddleware> _logger;

        public ApiBlockingMiddleware(
            RequestDelegate next,
            IApiBlocker blocker,
            TimeProvider timeProvider,
            ILogger<ApiBlockingMiddleware> logger)
        {
            _next = next;
            _blocker = blocker;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!_blocker.IsBlocked || IsExempt(context.Request.Path))
            {
                return _next(context);
            }

            return RefuseAsync(context);
        }

        private static bool IsExempt(PathString path)
        {
            foreach (var prefix in _exemptPrefixes)
            {
                if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private Task RefuseAsync(HttpContext context)
        {
            var untilUtc = _blocker.BlockedUntilUtc;

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            if (untilUtc is { } expiry)
            {
                var remaining = expiry - _timeProvider.GetUtcNow();

                // At least one second: a Retry-After of 0 invites an immediate retry, and a negative
                // value is not a legal header.
                var seconds = Math.Max(1, (long)Math.Ceiling(remaining.TotalSeconds));

                context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            }

            LogRefused(context.Request.Path.Value, _blocker.Reason);

            return Task.CompletedTask;
        }
    }
}
