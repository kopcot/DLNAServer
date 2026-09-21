namespace DlnaServer.Persistence.Entities
{
    /// <summary>
    /// Thumbnail image bytes, separated from <see cref="ThumbnailEntity"/> so thumbnail metadata can be
    /// queried without loading the blob.
    /// </summary>
    internal sealed class ThumbnailContentEntity : EntityBase
    {
        public int ThumbnailId { get; set; }

        public ThumbnailEntity? Thumbnail { get; set; }

        public required byte[] Data { get; set; }
    }
}
