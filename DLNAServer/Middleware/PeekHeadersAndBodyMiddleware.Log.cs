namespace DLNAServer.Middleware
{
    public partial class PeekHeadersAndBodyMiddleware
    {
        [LoggerMessage(1, LogLevel.Information, "Request - Headers:\n{headers}")]
        partial void InformationShowRequestHeaders(string headers);
        [LoggerMessage(2, LogLevel.Information, "Request - Body:\n{body}")]
        partial void InformationShowRequestBody(string body);
        [LoggerMessage(3, LogLevel.Information, "Response - Status code:\n{statusCode}")]
        partial void InformationShowResponseStatusCode(int statusCode);
        [LoggerMessage(4, LogLevel.Information, "Response - Headers:\n{headers}")]
        partial void InformationShowResponseHeaders(string headers);
        [LoggerMessage(5, LogLevel.Information, "Response - Body:\n{body}")]
        partial void InformationShowResponseBody(string body);
    }
}
