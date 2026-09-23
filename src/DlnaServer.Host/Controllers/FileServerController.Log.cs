namespace DlnaServer.Host.Controllers
{
    public sealed partial class FileServerController
    {
        // Information on the maintainer's decision of 2026-09-23: which media file is being served, and
        // whether it came from memory or the disc, is what an operator watching the log actually wants.
        // Two costs were weighed and accepted rather than overlooked, and both are still real. One line
        // per file served, kept for a rolling week, is a viewing history in plaintext on a share anyone
        // on the LAN can read - nothing here identifies a person, but the sequence over time is the
        // personal data. And a renderer issues one request per byte range, so a single film produces
        // hundreds of the second message: at Information these filled 32 MB of app.log in a day with
        // DebugMode off. Check the retention setting before assuming the log still reaches as far back.
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Serving '{FilePath}' from {Source}")]
        private partial void LogServingTransfer(string filePath, string source);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "Serving a byte range [{range}] of '{FilePath}' from {Source}")]
        private partial void LogServingRange(string filePath, string source, string range);

        // Thumbnails stay at Debug, which is the whole reason these two exist separately: a browsing
        // television asks for a preview per tile, so they outnumber the media lines by orders of
        // magnitude and none of them says anything about what is being watched.
        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Debug,
            Message = "Serving thumbnail '{FilePath}' from {Source}")]
        private partial void LogServingThumbnailTransfer(string filePath, string source);

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Debug,
            Message = "Serving a byte range [{range}] of thumbnail '{FilePath}' from {Source}")]
        private partial void LogServingThumbnailRange(string filePath, string source, string range);

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
