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

        public async Task InvokeAsync(HttpContext context)
        {
            if (_blockerService.IsBlocked)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync($"API is temporarily unavailable. Reason = '{_blockerService.Reason}'");
                return;
            }

            await _next(context);
        }
    }
}
