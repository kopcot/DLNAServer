using DlnaServer.Core.Dlna;

namespace DlnaServer.Persistence.Entities
{
    /// <summary>
    /// A media file in the indexed library, and the hub of the data model: metadata, subtitles and
    /// the thumbnail all hang off it by foreign key.
    /// </summary>
    internal sealed class MediaFileEntity : EntityBase
    {
        /// <summary>
        /// Absolute path on disk. Unique, and compared <b>case-sensitively</b>.
        /// </summary>
        /// <remarks>
        /// No collation, on purpose, unlike the <c>NOCASE</c> carried by <see cref="FileName"/>,
        /// <see cref="Title"/> and <see cref="Extension"/>. The target is Linux, where <c>Foo.mkv</c> and
        /// <c>foo.mkv</c> are two files - a case-insensitive unique index would silently reject the
        /// second, which is what the reference did. <c>MediaFileRepositoryTest</c> pins it, and
        /// <c>DlnaOptionsValidator</c>'s ordinal containment check depends on it.
        /// </remarks>
        public required string FullPath { get; set; }

        public required string FileName { get; set; }

        /// <summary>
        /// Title shown to renderers. Defaults to the file name without its extension.
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// File extension including the leading dot, lower case.
        /// </summary>
        public required string Extension { get; set; }

        public int? DirectoryId { get; set; }

        public MediaDirectoryEntity? Directory { get; set; }

        /// <summary>
        /// MIME advertised for this file. Comes from configuration when the extension is mapped there,
        /// which is how device quirks are expressed - notably .mp3 served as audio/mp4 for LG TVs.
        /// </summary>
        public DlnaMime Mime { get; set; }

        /// <summary>
        /// DLNA.ORG_PN profile advertised in <c>res@protocolInfo</c>. Null falls back to the MIME default.
        /// </summary>
        public string? DlnaProfileName { get; set; }

        public DlnaItemClass UpnpClass { get; set; }

        public long SizeInBytes { get; set; }

        public DateTime FileCreatedUtc { get; set; }

        public DateTime FileModifiedUtc { get; set; }

        /// <summary>
        /// Set when reading this file into the byte cache previously failed, so it is streamed straight
        /// from disk on every request instead of being retried.
        /// </summary>
        public bool IsExcludedFromCache { get; set; }

        /// <summary>
        /// Identifies the file's content as of the last scan, derived from its size and modification time.
        /// Comparing it against <see cref="MetadataStamp"/> and <see cref="ThumbnailStamp"/> is how a
        /// changed file gets reprocessed - the reference had no such check, so a replaced file kept its
        /// original metadata and thumbnail indefinitely.
        /// </summary>
        public required string ContentStamp { get; set; }

        /// <summary>
        /// The <see cref="ContentStamp"/> in force when metadata was last extracted. Null means never.
        /// </summary>
        public string? MetadataStamp { get; set; }

        /// <summary>
        /// The <see cref="ContentStamp"/> in force when the thumbnail was last generated. Null means never.
        /// </summary>
        public string? ThumbnailStamp { get; set; }

        /// <summary>
        /// Set when an operator cleared this file's metadata and does not want it produced again. The
        /// pending-work query skips such a file, which is the only thing that distinguishes clearing from
        /// recreating - both delete what is there, and without this the background pass would simply
        /// regenerate it.
        /// </summary>
        public bool IsMetadataSuppressed { get; set; }

        /// <summary>
        /// The thumbnail equivalent of <see cref="IsMetadataSuppressed"/>.
        /// </summary>
        public bool IsThumbnailSuppressed { get; set; }

        /// <summary>
        /// Set when an operator asked for this file's thumbnail to be produced again, so an existing
        /// image beside the media must not be adopted in place of generating one.
        /// </summary>
        /// <remarks>
        /// Adoption exists so an imported library arrives already previewed instead of costing a full
        /// ffmpeg pass, and it is keyed on the thumbnail being newer than the media. That makes a forced
        /// rebuild indistinguishable from a fresh import: clearing the row and the stamp left the file on
        /// disc, which was then adopted straight back. So "Recreate thumbnails" reported success and
        /// changed nothing at any setting - a raised MaxWidth never took effect. This flag is the
        /// difference, and SaveThumbnailAsync clears it once a real one has been produced.
        /// </remarks>
        public bool IsThumbnailRebuildForced { get; set; }

        /// <summary>
        /// Consecutive extraction failures, used to back off rather than retry an unreadable file on every browse.
        /// </summary>
        public int MetadataFailureCount { get; set; }

        public int ThumbnailFailureCount { get; set; }

        /// <summary>
        /// Every audio track found in the container. A film with several dubs has one row per language,
        /// where the reference - and this project until now - kept only the first.
        /// </summary>
        public ICollection<AudioStreamEntity> AudioStreams { get; set; } = [];

        public VideoStreamEntity? Video { get; set; }

        /// <summary>
        /// Every subtitle track found in the container. The reference kept only the first.
        /// </summary>
        public ICollection<SubtitleStreamEntity> Subtitles { get; set; } = [];

        /// <summary>
        /// Subtitle and lyrics files linked to this one, including automatic links the operator removed.
        /// </summary>
        public ICollection<SubtitleFileEntity> SubtitleFiles { get; set; } = [];

        public ThumbnailEntity? Thumbnail { get; set; }
    }
}
