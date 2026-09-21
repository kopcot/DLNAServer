using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// A generated preview image. Carries no image bytes - those are fetched separately so a listing
    /// never drags blobs into memory.
    /// </summary>
    public sealed record ThumbnailDto
    {
        public required Guid PublicId { get; init; }

        public required Guid MediaFilePublicId { get; init; }

        /// <summary>
        /// Absolute path of the generated image, always inside the configured thumbnail cache directory.
        /// </summary>
        public required string FilePath { get; init; }

        /// <summary>
        /// Absolute path of the media file this previews - <b>not</b> the image's own
        /// <see cref="FilePath"/>.
        /// </summary>
        /// <remarks>
        /// Optional rather than required, so no construction site had to change to gain it. It exists
        /// because thumbnails are listed and paged in the order of the media they belong to, and the
        /// keyset cursor a caller has to send back is therefore that path: <c>/manage/thumbnail</c> could
        /// accept an <c>after</c> and never tell a caller what the next one was, because the only path on
        /// this record was the <c>.@__thumb</c> image and paging on that would have paged over the wrong
        /// string entirely.
        /// </remarks>
        public string? MediaFileFullPath { get; init; }

        public required DlnaMime Mime { get; init; }

        public required int Width { get; init; }

        public required int Height { get; init; }

        public required long SizeInBytes { get; init; }

        /// <summary>
        /// True when a copy of the image is also stored in the database.
        /// </summary>
        public required bool HasStoredContent { get; init; }
    }
}
