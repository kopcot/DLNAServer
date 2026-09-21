using DlnaServer.Core.Configuration;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Reports every folder as unusable, so reconciliation's guard can be exercised.
    /// </summary>
    /// <remarks>
    /// A stand-in for the case the real checker reports off a live filesystem: an unmounted share, which
    /// is present, readable and empty. Producing one of those on demand is not portable between the
    /// Windows development box and the NAS - substituting the checker is, and it is the same seam
    /// <c>LibraryIndexer.FindUnusableSourceFolders</c> actually asks.
    /// </remarks>
    internal sealed class UnusableSourceFolderChecker : ISourceFolderChecker
    {
        public IReadOnlyList<SourceFolderCheck> Check(IReadOnlyCollection<string> paths)
        {
            ArgumentNullException.ThrowIfNull(paths);

            return [.. paths.Select(static path => new SourceFolderCheck
            {
                Path = path,
                Problem = "The folder could not be read.",
            })];
        }
    }
}
