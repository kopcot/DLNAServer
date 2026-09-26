using DlnaServer.Core.Contracts;

namespace DlnaServer.Persistence.Repositories
{
    /// <summary>
    /// Subtitle and lyrics files linked to media files: kept in step with the disc by each scan, and
    /// edited by the operator on a file's page.
    /// </summary>
    public interface ISubtitleRepository
    {
        /// <summary>
        /// Brings the automatic links in line with what a scan found.
        /// </summary>
        /// <param name="fileNamesByDirectory">
        /// Every subtitle-like file the scan saw, by the folder it is in, as file names without a folder.
        /// </param>
        /// <param name="excludedFolders">
        /// <c>Library.ExcludeFolders</c>, as the scan applied it. An automatic link into one of them is dropped
        /// without looking at the disc: the scan never walks there, so it can never be seen again.
        /// </param>
        /// <param name="isDefinitelyAbsent">
        /// Whether a file the scan did not see is really gone, rather than in a folder it could not read.
        /// An automatic link is only dropped for a file that is gone, or whose media no longer matches it.
        /// </param>
        /// <param name="cancellationToken">Stops the sync between its reads and its writes.</param>
        /// <returns>How many links were added and how many dropped.</returns>
        Task<(int Added, int Dropped)> SyncAutomaticAsync(
            IReadOnlyDictionary<string, HashSet<string>> fileNamesByDirectory,
            IReadOnlyList<string> excludedFolders,
            Func<string, bool> isDefinitelyAbsent,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The links of each of these media files, leaving out automatic links the operator removed.
        /// </summary>
        Task<IReadOnlyDictionary<Guid, IReadOnlyList<SubtitleFileDto>>> GetForFilesAsync(
            IReadOnlyCollection<Guid> mediaFilePublicIds,
            CancellationToken cancellationToken = default);

        Task<SubtitleFileDto?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Links a subtitle to a media file by hand. Its automatic links go, so the two never mix.
        /// </summary>
        /// <returns>False when the media file no longer exists.</returns>
        Task<bool> AddManualAsync(
            Guid mediaFilePublicId,
            string relativePath,
            string? language,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a link removed, whether found by name or added by hand, so the next scan leaves it out.
        /// </summary>
        Task RemoveAsync(Guid publicId, CancellationToken cancellationToken = default);

        Task SetLanguageAsync(Guid publicId, string? language, CancellationToken cancellationToken = default);
    }
}
