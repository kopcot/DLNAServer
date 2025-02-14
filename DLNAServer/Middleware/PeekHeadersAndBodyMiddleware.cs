namespace DLNAServer.Middleware
{
    public class PeekHeadersAndBodyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PeekHeadersAndBodyMiddleware> _logger;

        public PeekHeadersAndBodyMiddleware(RequestDelegate next, ILogger<PeekHeadersAndBodyMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }
        private static bool showInformation = true;
        public async Task InvokeAsync(HttpContext context)
        {
            // Peek into request headers
            var requestHeaders = context.Request.Headers;

            // If needed, log the request headers or body (be careful with sensitive data)
            var headers = context.Request.Headers.Select(h => $"{h.Key}: {h.Value}").ToList();
            //showInformation = headers.Any(h => h.Contains("SOAP"));
            if (showInformation)
            {
                _logger.LogInformation($"Request Headers: {Environment.NewLine}{string.Join(Environment.NewLine, headers)}");
            }

            // Peek into request body without consuming it
            context.Request.EnableBuffering(); // Allows the body to be read multiple times
            var requestBody = await ReadRequestBody(context.Request);
            if (showInformation)
            {
                _logger.LogInformation($"Request Body: {Environment.NewLine}{requestBody}");
            }

            context.Request.Body.Position = 0; // Reset the stream position for the next component in the pipeline to read it

            // Call the next middleware in the pipeline
            using (var originalResponseBodyStream = context.Response.Body)
            using (var responseBodyStream = new MemoryStream())
            {
                // Replace the response body stream with a new one to peek into it
                context.Response.Body = responseBodyStream;

                await _next(context); // Call the next middleware in the pipeline

                // Peek into response headers
                if (showInformation)
                {
                    _logger.LogInformation($"Sending response: {Environment.NewLine}{context.Response.StatusCode}");
                }

                var responseHeaders = context.Response.Headers.Select(h => $"{h.Key}: {h.Value}").ToList();
                if (showInformation)
                {
                    _logger.LogInformation($"Response Headers: {Environment.NewLine}{string.Join(Environment.NewLine, headers)}");
                }

                // Peek into response body
                var responseBody = await ReadResponseBody(context.Response);
                if (showInformation)
                {
                    _logger.LogInformation($"ResponseBody: {Environment.NewLine}{responseBody}");
                }

                responseBodyStream.Position = 0; // Reset the stream position

                // Copy the modified response body back to the original stream
                await responseBodyStream.CopyToAsync(originalResponseBodyStream);
            }
        }

        private async Task<string> ReadRequestBody(HttpRequest request)
        {
            request.EnableBuffering(); // Enables multiple reads of the request body
            using (var reader = new StreamReader(request.Body, leaveOpen: true))
            {
                var body = await reader.ReadToEndAsync();
                return body;
            }
        }

        private async Task<string> ReadResponseBody(HttpResponse response)
        {
            _ = response.Body.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(response.Body, leaveOpen: true))
            {
                var body = await reader.ReadToEndAsync();
                return body;
            }
        }
    }
}
