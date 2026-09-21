namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// What one candidate source folder turned out to be when it was looked at on disc.
    /// </summary>
    public sealed record SourceFolderCheck
    {
        /// <summary>
        /// The path exactly as it was entered, so a result can be matched back to its line.
        /// </summary>
        public required string Path { get; init; }

        /// <summary>
        /// What is wrong with the path, or <see langword="null"/> when it is usable as a source folder.
        /// </summary>
        public string? Problem { get; init; }

        /// <summary>
        /// Whether the folder is readable but holds nothing at all.
        /// </summary>
        /// <remarks>
        /// Deliberately separate from <see cref="Problem"/>, because the two callers need opposite
        /// answers. An empty folder is a perfectly good thing to configure - the operator may be about to
        /// fill it - so the admin UI must not call it broken. Reconciliation must, because an unmounted
        /// share is present, readable and empty, and "every file is gone" is exactly what it looks like.
        /// </remarks>
        public bool IsEmpty { get; init; }

        public bool IsUsable => Problem is null;

        /// <summary>
        /// Whether reconciliation may delete indexed rows that this folder no longer appears to hold.
        /// </summary>
        public bool IsSafeToReconcile => IsUsable && !IsEmpty;
    }
}
