namespace DlnaServer.Media.Processing.Provisioning
{
    /// <summary>
    /// Makes ffmpeg and ffprobe available, or reports that they are not.
    /// </summary>
    public interface IFFmpegProvisioner
    {
        /// <summary>
        /// True when ffmpeg and ffprobe can be used. Resolved once and cached.
        /// </summary>
        /// <param name="allowDownload">Fetch the binaries if they are absent.</param>
        /// <param name="cancellationToken">Cancels an in-flight download.</param>
        Task<bool> EnsureAvailableAsync(bool allowDownload, CancellationToken cancellationToken = default);
    }
}
