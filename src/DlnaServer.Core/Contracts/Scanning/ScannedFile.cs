using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Contracts.Scanning
{
    /// <summary>
    /// A media file found on disk by a library scan, before it is compared against the index.
    /// </summary>
    /// <remarks>
    /// Carries only what a scan can learn from the filesystem. Metadata and thumbnails come later, from
    /// the background processing queue.
    /// </remarks>
    public sealed record ScannedFile
    {
        public required string FullPath { get; init; }

        public required string FileName { get; init; }

        /// <summary>
        /// Directory holding the file, as an absolute path.
        /// </summary>
        public required string DirectoryPath { get; init; }

        /// <summary>
        /// Extension including the leading dot, lower case.
        /// </summary>
        public required string Extension { get; init; }

        public required DlnaMime Mime { get; init; }

        /// <summary>
        /// DLNA profile from configuration, when the extension mapping specified one.
        /// </summary>
        public string? DlnaProfileName { get; init; }

        public required long SizeInBytes { get; init; }

        /// <summary>
        /// The date to treat as the file's own, honouring <c>Library.UseFileCreationDateTime</c>.
        /// </summary>
        public required DateTime CreatedUtc { get; init; }

        /// <summary>
        /// The best date the filesystem itself reports for this file, whatever the configuration says.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="CreatedUtc"/>, which is the configured choice and is <c>UtcNow</c>
        /// when <c>UseFileCreationDateTime</c> is off. This one is needed for the very first fill of an
        /// empty index, where "when was this indexed" carries no information at all: every row would
        /// share one timestamp and Recently added would degenerate into insertion order.
        /// </remarks>
        public required DateTime FileSystemCreatedUtc { get; init; }

        public required DateTime ModifiedUtc { get; init; }

        /// <summary>
        /// Size and modification time combined, compared against the stored stamp to detect a changed file.
        /// </summary>
        public required string ContentStamp { get; init; }
    }
}
