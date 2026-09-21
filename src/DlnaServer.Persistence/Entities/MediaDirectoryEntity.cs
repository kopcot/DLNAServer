namespace DlnaServer.Persistence.Entities
{
    /// <summary>
    /// A folder in the indexed library. Directories form a tree via <see cref="ParentDirectoryId"/>
    /// and surface to renderers as DIDL-Lite containers.
    /// </summary>
    internal sealed class MediaDirectoryEntity : EntityBase
    {
        /// <summary>
        /// Absolute path on disk. Unique, and compared <b>case-sensitively</b>.
        /// </summary>
        /// <remarks>
        /// No collation, on purpose, unlike the <c>NOCASE</c> carried by <see cref="Name"/>. The target is
        /// Linux, where two paths differing only in case are two different directories - a
        /// case-insensitive unique index would silently reject the second of them, which is what the
        /// reference did. <c>LibraryIndexer.NormaliseSourceFolders</c> and
        /// <c>DlnaOptionsValidator</c> both compare ordinally for the same reason.
        /// </remarks>
        public required string FullPath { get; set; }

        /// <summary>
        /// Final path segment, shown as the container title.
        /// </summary>
        public required string Name { get; set; }

        public int? ParentDirectoryId { get; set; }

        public MediaDirectoryEntity? ParentDirectory { get; set; }

        public ICollection<MediaDirectoryEntity> Subdirectories { get; set; } = [];

        public ICollection<MediaFileEntity> Files { get; set; } = [];

        /// <summary>
        /// Depth below the filesystem root, denormalised so subtree queries do not have to walk parents.
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// True when this directory is one of the configured source folders, making it a top-level
        /// container in the DLNA root. Stored rather than recomputed by comparing paths against
        /// configuration on every Browse, which is what the reference did.
        /// </summary>
        public bool IsSourceRoot { get; set; }
    }
}
