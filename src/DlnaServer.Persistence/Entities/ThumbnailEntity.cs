using DlnaServer.Core.Dlna;

namespace DlnaServer.Persistence.Entities
{
    /// <summary>
    /// A generated preview image for a media file. The image always exists as a file in the thumbnail
    /// cache directory; <see cref="Content"/> optionally holds a second copy in the database.
    /// </summary>
    internal sealed class ThumbnailEntity : EntityBase
    {
        public int MediaFileId { get; set; }

        public MediaFileEntity? MediaFile { get; set; }

        /// <summary>
        /// Absolute path of the generated image, always inside the configured thumbnail cache directory
        /// and therefore never inside the media tree.
        /// </summary>
        public required string FilePath { get; set; }

        public DlnaMime Mime { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public long SizeInBytes { get; set; }

        /// <summary>
        /// The image bytes, present only when database storage is enabled. Held in its own table so
        /// listing thumbnails does not drag every blob into memory.
        /// </summary>
        /// <remarks>
        /// This side owns the relationship: the foreign key lives on <see cref="ThumbnailContentEntity"/>,
        /// so deleting a thumbnail cascades into its blob. It used to be the other way round, which meant
        /// every deleted thumbnail - and every deleted media file, through its own cascade - left its
        /// bytes behind unreachable, in a database that is never vacuumed.
        /// </remarks>
        public ThumbnailContentEntity? Content { get; set; }
    }
}
