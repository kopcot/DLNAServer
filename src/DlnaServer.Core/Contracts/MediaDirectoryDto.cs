namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// A directory in the indexed library, surfaced to renderers as a DIDL-Lite container.
    /// </summary>
    public sealed record MediaDirectoryDto
    {
        /// <summary>
        /// Stable external identifier, used as the DIDL-Lite container ObjectID.
        /// </summary>
        public required Guid PublicId { get; init; }

        public required string FullPath { get; init; }

        /// <summary>
        /// Final path segment, shown as the container title.
        /// </summary>
        public required string Name { get; init; }

        public Guid? ParentDirectoryPublicId { get; init; }

        public required int Depth { get; init; }

        /// <summary>
        /// True when this directory is a configured source folder, making it a top-level container
        /// in the DLNA root.
        /// </summary>
        public required bool IsSourceRoot { get; init; }

        public required DateTime CreatedUtc { get; init; }
    }
}
