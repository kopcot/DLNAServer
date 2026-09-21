namespace DlnaServer.Host.Diagnostics
{
    internal sealed partial class ApiBlockingMiddleware
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Refused '{Path}' with 503; requests are blocked ({Reason})")]
        private partial void LogRefused(string? path, string? reason);
    }
}
