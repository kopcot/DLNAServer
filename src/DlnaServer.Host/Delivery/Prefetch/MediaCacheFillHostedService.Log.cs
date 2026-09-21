namespace DlnaServer.Host.Delivery.Prefetch
{
    internal sealed partial class MediaCacheFillHostedService
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "'{FilePath}' ({SizeInBytes} bytes) is in the served-bytes cache, read from {Source}")]
        private partial void LogFilled(string filePath, int sizeInBytes, string source);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "'{FilePath}' will not be cached; every request for it streams from disc")]
        private partial void LogExcluded(string filePath);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Error,
            Message = "Filling the served-bytes cache from '{FilePath}' failed; the backlog continues")]
        private partial void LogFillFailed(string filePath, Exception exception);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "'{FilePath}' could not be cached this time and was left alone. Only a file that is "
                + "genuinely too large is recorded as uncacheable - a read that merely failed must not "
                + "send it to the platter for good.")]
        private partial void LogFillDeferred(string filePath);
    }
}
