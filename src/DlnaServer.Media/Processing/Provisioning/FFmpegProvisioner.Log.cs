namespace DlnaServer.Media.Processing.Provisioning
{
    internal sealed partial class FFmpegProvisioner
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Using ffmpeg from '{Directory}'")]
        private partial void LogUsingExisting(string directory);

        // Warning, because this is the one thing the server does that runs code it did not ship. The
        // downloader carries no hash, checksum or signature and pins no version, so TLS is the only
        // control and a compromised upstream is unopposed. Installing ffmpeg by hand is the supported
        // path; see NasBuild.usage.txt.
        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "ffmpeg not found; downloading it into '{Directory}' because "
                + "Thumbnails.DownloadFFmpeg is enabled. The download is not integrity-verified and is "
                + "then executed - installing ffmpeg by hand and disabling this is the supported path.")]
        private partial void LogDownloading(string directory);

        [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "ffmpeg downloaded to '{Directory}'")]
        private partial void LogDownloaded(string directory);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "ffmpeg is unavailable ({Reason}). Indexing, browsing and streaming continue; "
                + "video metadata and video thumbnails will be skipped.")]
        private partial void LogUnavailable(string reason);
    }
}
