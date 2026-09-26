using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Contracts.Scanning
{
    /// <summary>
    /// Inputs for one library scan, resolved from configuration.
    /// </summary>
    public sealed record LibraryScanRequest
    {
        /// <summary>
        /// Whether a file's indexed creation date comes from the file itself rather than from the scan.
        /// </summary>
        /// <remarks>
        /// Off by default, which is what makes "recently added" work for an imported library: every file
        /// copied onto the NAS carries its original creation date, often years old, so ordering by it
        /// shows nothing as recent. It also sidesteps Linux, where a creation date depends on the
        /// filesystem exposing a birth time and can be meaningless.
        /// </remarks>
        public bool UseFileCreationDateTime { get; init; }

        /// <summary>
        /// Absolute paths of the folders to walk.
        /// </summary>
        public required IReadOnlyList<string> SourceFolders { get; init; }

        /// <summary>
        /// Folder names to skip, matched as whole path segments.
        /// </summary>
        public required IReadOnlyList<string> ExcludedFolderNames { get; init; }

        /// <summary>
        /// Extension to MIME mapping from configuration. This wins over the built-in catalog, because it
        /// is where device quirks are expressed - notably .mp3 served as audio/mp4 for LG TVs.
        /// </summary>
        public required IReadOnlyDictionary<string, MediaExtensionMapping> ExtensionMappings { get; init; }

        /// <summary>
        /// The subtitle and lyrics types from configuration, keyed case-insensitively, each with the kind of
        /// media it is linked to. A file of one of these types is reported as a subtitle rather than skipped.
        /// </summary>
        public required IReadOnlyDictionary<string, DlnaMedia> SubtitleTypes { get; init; }
    }
}
