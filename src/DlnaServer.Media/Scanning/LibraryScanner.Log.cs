namespace DlnaServer.Media.Scanning
{
    internal sealed partial class LibraryScanner
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "Source folder '{SourceFolder}' does not exist and was skipped")]
        private partial void LogSourceFolderMissing(string sourceFolder);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Skipped unreadable file '{Path}' ({Reason})")]
        private partial void LogFileUnreadable(string path, string reason);
    }
}
