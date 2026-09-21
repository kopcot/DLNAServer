namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// Which indexed files an operation applies to: one file, or the files of one directory.
    /// </summary>
    /// <remarks>
    /// Selecting the files and deciding what to do to them are separate concerns, so this carries only
    /// the selection and every operation that acts on a set of files takes one. Use <see cref="ForFile"/> or
    /// <see cref="ForDirectory"/> rather than building one by hand - a scope naming neither, or both,
    /// has no meaning.
    /// </remarks>
    public sealed record MediaFileScope
    {
        private MediaFileScope()
        {
        }

        public Guid? FilePublicId { get; private init; }

        public Guid? DirectoryPublicId { get; private init; }

        /// <summary>
        /// Whether a directory scope reaches the whole tree beneath it rather than its own files alone.
        /// </summary>
        public bool IncludeSubdirectories { get; private init; }

        public static MediaFileScope ForFile(Guid filePublicId)
        {
            return new MediaFileScope { FilePublicId = filePublicId };
        }

        public static MediaFileScope ForDirectory(Guid directoryPublicId, bool includeSubdirectories)
        {
            return new MediaFileScope
            {
                DirectoryPublicId = directoryPublicId,
                IncludeSubdirectories = includeSubdirectories,
            };
        }
    }
}
