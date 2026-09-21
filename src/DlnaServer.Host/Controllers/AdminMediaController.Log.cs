namespace DlnaServer.Host.Controllers
{
    public sealed partial class AdminMediaController
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "The admin preview asked for '{FilePath}', which is indexed but no longer on disc")]
        private partial void LogMissing(string filePath);
    }
}
