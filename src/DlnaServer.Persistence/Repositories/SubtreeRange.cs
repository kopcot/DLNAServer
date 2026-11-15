using DlnaServer.Core.Files;
using DlnaServer.Persistence.Entities;

namespace DlnaServer.Persistence.Repositories
{
    /// <summary>
    /// The range of stored paths lying beneath a folder - one home for the subtree bound every
    /// repository query uses.
    /// </summary>
    /// <remarks>
    /// A half-open range rather than a <c>LIKE 'path%'</c> prefix, so SQLite can seek the unique index on
    /// <c>FullPath</c>: everything beneath a folder sorts after the folder plus its separator and before the
    /// folder plus the character after that separator, which leaves a sibling such as <c>Media2</c> outside.
    /// <para>
    /// The folder's own trailing separator is trimmed before one is appended. A root such as <c>/</c> or
    /// <c>D:\</c> keeps its separator through <see cref="Path.TrimEndingDirectorySeparator(string)"/>, and a
    /// doubled one bounds a range nothing falls in. This rule lived in three copies and only two trimmed, so
    /// a root source folder vanished from every listing.
    /// </para>
    /// </remarks>
    internal static class SubtreeRange
    {
        private const char PosixSeparator = '/';
        private const char WindowsSeparator = '\\';

        // Each separator, then the character after it in code-point order: 0 is / plus one, ] is \ plus one.
        private const string PosixStart = "/";
        private const string PosixEnd = "0";
        private const string WindowsStart = "\\";
        private const string WindowsEnd = "]";

        /// <summary>
        /// The bounds for a folder whose path is known: after <c>Start</c> and before <c>End</c>.
        /// </summary>
        /// <remarks>
        /// The separator is read from the path rather than from this host, because the rows carry the
        /// separator of whichever machine indexed them.
        /// </remarks>
        internal static (string Start, string End) Of(string folderPath)
        {
            var separator = StoredPath.SeparatorOf(folderPath);
            var trimmed = folderPath.TrimEnd(separator);

            return (trimmed + separator, trimmed + (char)(separator + 1));
        }

        /// <summary>
        /// Keeps the folders that have at least one of <paramref name="files"/> beneath them.
        /// </summary>
        /// <remarks>
        /// The same bounds as <see cref="Of"/>, written into the query because the folder path is a column
        /// here rather than a value. Both separators are tried, since the folder's own is not known until
        /// the row is read; the trim is SQLite's <c>rtrim</c>.
        /// </remarks>
        internal static IQueryable<MediaDirectoryEntity> WithAnyFileBeneath(
            IQueryable<MediaDirectoryEntity> folders,
            IQueryable<MediaFileEntity> files)
        {
            return folders.Where(d => files.Any(f =>
                (string.Compare(f.FullPath, d.FullPath.TrimEnd(PosixSeparator) + PosixStart) > 0
                    && string.Compare(f.FullPath, d.FullPath.TrimEnd(PosixSeparator) + PosixEnd) < 0)
                || (string.Compare(f.FullPath, d.FullPath.TrimEnd(WindowsSeparator) + WindowsStart) > 0
                    && string.Compare(f.FullPath, d.FullPath.TrimEnd(WindowsSeparator) + WindowsEnd) < 0)));
        }
    }
}
