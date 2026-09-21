namespace DlnaServer.Host.Indexing
{
    internal sealed partial class MediaProcessingHostedService
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Thumbnail created for '{FilePath}' at {Width}x{Height}")]
        private partial void LogThumbnailCreated(string filePath, int width, int height);

        /// <summary>
        /// Debug, not Information: an adopted thumbnail is the normal case on a library the reference has
        /// already previewed, so at Information a first pass would log a line per file for work it did
        /// not do.
        /// </summary>
        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Debug,
            Message = "Adopted the existing thumbnail for '{FilePath}'")]
        private partial void LogThumbnailAdopted(string filePath);

        /// <summary>
        /// Mirrors the reference's <c>Set metadata for file: '{file}'</c>, which it logs at Information
        /// for audio and video. Without it a successful probe left no trace at any level, and the only
        /// evidence ffprobe worked at all was the absence of a warning.
        /// </summary>
        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Information,
            Message = "Stored metadata for '{FilePath}'")]
        private partial void LogMetadataStored(string filePath);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Error,
            Message = "Reading metadata for '{FilePath}' threw. It counts as a failed attempt.")]
        private partial void LogMetadataFailed(string filePath, Exception exception);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Error,
            Message = "Generating a thumbnail for '{FilePath}' threw. It counts as a failed attempt.")]
        private partial void LogThumbnailFailed(string filePath, Exception exception);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Error,
            Message = "Processing '{FilePath}' failed outside metadata and thumbnail work; the file is skipped")]
        private partial void LogFileFailed(string filePath, Exception exception);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Error,
            Message = "A media processing pass failed; the service continues and the next pass retries")]
        private partial void LogPassFailed(Exception exception);

        /// <summary>
        /// The evidence that a run's borrowed memory came back. Information, not Debug, because it is
        /// the only record that the bargain in this class's remarks was honoured.
        /// </summary>
        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Information,
            Message = "Previews finished; memory settled from {BeforeMegabytes} MB to {AfterMegabytes} MB")]
        private partial void LogSettled(long beforeMegabytes, long afterMegabytes);

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Warning,
            Message = "Releasing the index memory after a processing run failed; the collection still ran")]
        private partial void LogSettleFailed(Exception exception);
    }
}
