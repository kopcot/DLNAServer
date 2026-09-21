using DlnaServer.Core.Dlna;

namespace DlnaServer.Media.Processing
{
    /// <summary>
    /// Settings for one processing pass, resolved from configuration.
    /// </summary>
    /// <param name="MaxWidth">Thumbnail bounding width.</param>
    /// <param name="MaxHeight">Thumbnail bounding height.</param>
    /// <param name="Quality">Encoder quality, 1-100.</param>
    /// <param name="ThumbnailMime">Format thumbnails are encoded in.</param>
    /// <param name="StoreThumbnailContent">Keep a copy of the image bytes in the database.</param>
    /// <param name="AllowFFmpegDownload">Fetch ffmpeg on first use if it is not present.</param>
    /// <param name="ReadContainerTags">
    /// Also read every tag the file carries about itself. Costs a second ffprobe call per file, which is
    /// why it can be turned off for a large library that does not need them.
    /// </param>
    public sealed record MediaProcessingSettings(
        int MaxWidth,
        int MaxHeight,
        int Quality,
        DlnaMime ThumbnailMime,
        bool StoreThumbnailContent,
        bool AllowFFmpegDownload,
        bool ReadContainerTags);
}
