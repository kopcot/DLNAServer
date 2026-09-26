using DlnaServer.Core.Configuration;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Reports every folder usable for the first <c>usableChecks</c> calls and unusable after that.
    /// </summary>
    /// <remarks>
    /// Stands in for a share that unmounts while a pass is running: the gate at the start of the pass
    /// sees it, the checks reconciliation makes later do not. Same seam as
    /// <see cref="UnusableSourceFolderChecker"/>.
    /// </remarks>
    internal sealed class LaterUnusableSourceFolderChecker : ISourceFolderChecker
    {
        private readonly int _usableChecks;
        private int _checks;

        public LaterUnusableSourceFolderChecker(int usableChecks)
        {
            _usableChecks = usableChecks;
        }

        public IReadOnlyList<SourceFolderCheck> Check(IReadOnlyCollection<string> paths)
        {
            ArgumentNullException.ThrowIfNull(paths);

            var problem = ++_checks > _usableChecks
                ? "The folder could not be read."
                : null;

            return [.. paths.Select(path => new SourceFolderCheck { Path = path, Problem = problem })];
        }
    }
}
