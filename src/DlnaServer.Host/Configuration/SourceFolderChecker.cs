using DlnaServer.Core.Configuration;

namespace DlnaServer.Host.Configuration
{
    /// <inheritdoc cref="ISourceFolderChecker"/>
    internal sealed class SourceFolderChecker : ISourceFolderChecker
    {
        public IReadOnlyList<SourceFolderCheck> Check(IReadOnlyCollection<string> paths)
        {
            ArgumentNullException.ThrowIfNull(paths);

            var results = new List<SourceFolderCheck>(paths.Count);

            foreach (var path in paths)
            {
                var problem = FindProblem(path, out var isEmpty);

                results.Add(new SourceFolderCheck
                {
                    Path = path,
                    Problem = problem,
                    IsEmpty = isEmpty,
                });
            }

            return results;
        }

        /// <remarks>
        /// Ordered cheapest first, and each step assumes the ones above it passed - so a malformed path is
        /// never handed to the filesystem, and "no such folder" is only ever said about a path that could
        /// have named one.
        /// </remarks>
        private static string? FindProblem(string path, out bool isEmpty)
        {
            isEmpty = false;

            if (string.IsNullOrWhiteSpace(path))
            {
                return "Blank.";
            }

            var trimmed = path.Trim();

            try
            {
                // Rejects the characters a path cannot hold, on whichever platform this is running.
                _ = Path.GetFullPath(trimmed);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // The framework's own text names the offending character and the API that rejected it,
                // and the Settings page renders whatever is returned here straight to the operator.
                return "This is not a valid folder path.";
            }

            if (!Path.IsPathRooted(trimmed))
            {
                // A relative folder resolves against the process's working directory, which is not the
                // same place when the server is started by hand and when the NAS starts it.
                return "Not an absolute path, so where it points depends on how the server was started.";
            }

            if (!Directory.Exists(trimmed))
            {
                return File.Exists(trimmed)
                    ? "This is a file, not a folder."
                    : "No such folder.";
            }

            try
            {
                // Existence is not readability, and on a NAS a share the server cannot read looks exactly
                // like one that is present and empty. Reading one entry is what tells them apart.
                using var entries = Directory.EnumerateFileSystemEntries(trimmed).GetEnumerator();

                // The result is what separates "present and empty" from "present and full", which is not
                // a problem here but is the difference between a reconcile pass and a destroyed index.
                // Reported through SourceFolderCheck.IsEmpty rather than as a problem, because an empty
                // folder is a legitimate thing to configure.
                isEmpty = !entries.MoveNext();
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                return "This folder exists but cannot be opened - check that the server is allowed to read it.";
            }

            return null;
        }
    }
}
