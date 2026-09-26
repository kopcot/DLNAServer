namespace DlnaServer.Host.Controllers
{
    public sealed partial class AdminMediaController
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "The admin preview asked for '{FilePath}', which is indexed but no longer on disc")]
        private partial void LogMissing(string filePath);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "The subtitle '{FilePath}' could not be read for a download, and was left out")]
        private partial void LogSubtitleSkipped(string filePath, Exception exception);
    }
}
