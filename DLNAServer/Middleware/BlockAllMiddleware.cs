using DLNAServer.Features.ApiBlocking.Interfaces;

namespace DLNAServer.Middleware
{
    public class BlockAllMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IApiBlockerService _blockerService;

        public BlockAllMiddleware(RequestDelegate next, IApiBlockerService blockerService)
        {
            _next = next;
            _blockerService = blockerService;
        }

        public Task InvokeAsync(HttpContext context)
        {
            if (_blockerService.IsBlocked)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return context.Response.WriteAsync(string.Format("API is temporarily unavailable. Reason = '{0}'", [_blockerService.Reason]));
            }

            return _next(context);
        }
    }
}
