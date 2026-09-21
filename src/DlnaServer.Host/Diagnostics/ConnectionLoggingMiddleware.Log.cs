namespace DlnaServer.Host.Diagnostics
{
    internal sealed partial class ConnectionLoggingMiddleware
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "{Method} {Path}{QueryString} from {RemoteAddress}:{RemotePort} "
                + "to {LocalAddress}:{LocalPort}, User-Agent: '{UserAgent}', Range: '{Range}'")]
        private partial void LogRequestReceived(
            string method,
            string path,
            string queryString,
            string? remoteAddress,
            int remotePort,
            string? localAddress,
            int localPort,
            string userAgent,
            string range);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "{Method} {Path} -> {StatusCode}, {ContentLength} byte(s)")]
        private partial void LogResponseSent(string method, string path, int statusCode, long? contentLength);
    }
}
