using DlnaServer.Core.Dlna;

namespace DlnaServer.Media.Processing.Thumbnails
{
    /// <summary>
    /// Settings for generating one thumbnail.
    /// </summary>
    /// <param name="MaxWidth">Bounding width. The source is only ever scaled down.</param>
    /// <param name="MaxHeight">Bounding height.</param>
    /// <param name="Quality">Encoder quality, 1-100.</param>
    /// <param name="Mime">Format to encode in.</param>
    /// <param name="StoreContent">
    /// Also return the bytes for storage in the database. False keeps a bulk generation pass from
    /// carrying every image in memory.
    /// </param>
    public sealed record ThumbnailRequest(
        int MaxWidth,
        int MaxHeight,
        int Quality,
        DlnaMime Mime,
        bool StoreContent);
}
