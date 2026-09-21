using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Scanning;

namespace DlnaServer.Media.Scanning
{
    /// <summary>
    /// Walks the configured source folders and reports what is on disk.
    /// </summary>
    /// <remarks>
    /// Knows nothing about the index. It reports what exists; deciding what is new, changed or gone is
    /// the indexer's job. That split is what keeps this testable against a temporary folder with no
    /// database in sight.
    /// </remarks>
    public interface ILibraryScanner
    {
        /// <summary>
        /// Streams every media file under the source folders.
        /// </summary>
        /// <remarks>
        /// Lazy on purpose: a 20,000-file library must never be held in memory in one list. The caller
        /// batches as it consumes.
        /// </remarks>
        IEnumerable<ScannedFile> EnumerateFiles(
            LibraryScanRequest options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams every directory under the source folders, source roots included, parents before children.
        /// </summary>
        IEnumerable<string> EnumerateDirectories(
            LibraryScanRequest options,
            CancellationToken cancellationToken = default);
    }
}
