using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Processing;
using DlnaServer.Core.Dlna;

namespace DlnaServer.Media.Processing
{
    /// <summary>
    /// Extracts metadata and generates thumbnails for one media file at a time.
    /// </summary>
    /// <remarks>
    /// Single-file by design. Thumbnail work allocates natively through SkiaSharp and ffmpeg, so running
    /// several at once multiplies resident memory rather than throughput on a NAS. The caller controls
    /// concurrency, and today it is one.
    /// </remarks>
    public interface IMediaProcessor
    {
        /// <summary>
        /// Reads stream metadata. Returns null when the file could not be probed, so the caller can
        /// count the failure and back off; an image returns an empty result rather than null.
        /// </summary>
        Task<MediaMetadataResult?> ExtractMetadataAsync(
            string filePath,
            DlnaMime mime,
            MediaProcessingSettings settings,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes a thumbnail to <paramref name="targetPath"/>. Returns null when none could be produced.
        /// </summary>
        /// <remarks>
        /// <c>knownDuration</c> is the video's duration when the caller already has it, so a second
        /// ffprobe is not spawned to re-read it. Both this and <see cref="ExtractMetadataAsync"/> probed
        /// the same file in the same pass, which on a cold index is 25,000 redundant process spawns -
        /// each a fork, an exec and a container-header seek on a platter. Null still means "probe",
        /// which is what the thumbnail-only paths pass.
        /// </remarks>
        Task<GeneratedThumbnail?> GenerateThumbnailAsync(
            string filePath,
            DlnaMime mime,
            string targetPath,
            MediaProcessingSettings settings,
            bool allowAdoption = true,
            DateTime? notOlderThanUtc = null,
            TimeSpan? knownDuration = null,
            CancellationToken cancellationToken = default);
    }
}
