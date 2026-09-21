namespace DlnaServer.Media.Processing
{
    internal sealed partial class MediaProcessor
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "Metadata extraction for '{FilePath}' failed ({Reason})")]
        private partial void LogMetadataFailed(string filePath, string reason);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "Thumbnail for '{FilePath}' failed ({Reason})")]
        private partial void LogThumbnailFailed(string filePath, string reason);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Thumbnail conversion for '{FilePath}' failed ({Reason})")]
        private partial void LogThumbnailConversionFailed(string filePath, string reason);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "The temporary frame '{FramePath}' could not be deleted ({Reason}); "
                + "the next attempt overwrites it")]
        private partial void LogFrameCleanupFailed(string framePath, string reason);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Warning,
            Message = "'{FilePath}' was not passed to ffmpeg: the name contains a quote or a line break, "
                + "which would inject further ffmpeg arguments. Rename the file to have it processed")]
        private partial void LogUnsafePathRefused(string filePath);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Warning,
            Message = "ffmpeg gave no answer for '{FilePath}' within {TimeoutSeconds}s and was abandoned. "
                + "Without a timeout this call had nothing to cancel it before shutdown, so one such file "
                + "stopped all metadata and thumbnail work for the life of the process.")]
        private partial void LogFFmpegTimedOut(string filePath, int timeoutSeconds);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Debug,
            Message = "'{FilePath}' carries no embedded cover image, so it has no preview of its own")]
        private partial void LogNoEmbeddedArtwork(string filePath);

        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Debug,
            Message = "The tags in '{FilePath}' could not be read ({Reason}); "
                + "its other metadata is unaffected")]
        private partial void LogContainerTagsFailed(string filePath, string reason);
    }
}
