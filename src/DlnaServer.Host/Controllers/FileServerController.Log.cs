namespace DlnaServer.Host.Controllers
{
    public sealed partial class FileServerController
    {
        // Debug, not Information: one line per file served, kept for a rolling week, is a viewing
        // history in plaintext on a share anyone on the LAN can read. Nothing here identifies a person,
        // but the sequence over time is the personal data - and the volume argument points the same way,
        // so the privacy fix and keeping app.log readable are one change.
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Serving '{FilePath}' from {Source}")]
        private partial void LogServingTransfer(string filePath, string source);

        // Debug, not Information: a renderer issues one request per byte range, so a single film produces
        // hundreds of these. At Information they filled 32 MB of app.log in a day with DebugMode off,
        // which is what the controller's own documentation already said should not happen.
        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Serving a byte range [{range}] of '{FilePath}' from {Source}")]
        private partial void LogServingRange(string filePath, string source, string range);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "File {PublicId} was requested by {RemoteAddress} but is not in the index")]
        private partial void LogFileNotIndexed(Guid publicId, string? remoteAddress);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "Indexed file '{FilePath}' no longer exists on disc")]
        private partial void LogFileMissing(string filePath);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Warning,
            Message = "Thumbnail {PublicId} was requested by {RemoteAddress} but is not in the index")]
        private partial void LogThumbnailNotIndexed(Guid publicId, string? remoteAddress);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Warning,
            Message = "Thumbnail file '{FilePath}' is recorded but no longer exists on disc")]
        private partial void LogThumbnailMissing(string filePath);

        // Debug: a renderer probes before most transfers, so this is as frequent as the transfers
        // themselves and carries no information the transfer line does not.
        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Debug,
            Message = "Answered a HEAD probe for '{FilePath}' without reading it")]
        private partial void LogProbed(string filePath);
    }
}
