namespace DlnaServer.Core.Files
{
    /// <summary>
    /// Reads facts off a path that came out of the database rather than off the running host.
    /// </summary>
    /// <remarks>
    /// Stored paths were written by whichever machine indexed the library, and this server runs on Linux
    /// in production and Windows in development. Anything that rebuilds or compares a stored path has to
    /// take its separator from the path itself; taking it from
    /// <see cref="System.IO.Path.DirectorySeparatorChar"/> matches nothing the moment a database is
    /// opened on the other operating system, which is silent and looks like a data problem rather than a
    /// path-handling one.
    /// </remarks>
    public static class StoredPath
    {
        /// <summary>
        /// The separator a stored path is actually built from.
        /// </summary>
        /// <remarks>
        /// Falls back to the host's separator only when the path holds no separator at all, in which case
        /// there is nothing to disagree with.
        /// </remarks>
        public static char SeparatorOf(string fullPath)
        {
            ArgumentNullException.ThrowIfNull(fullPath);

            var index = fullPath.LastIndexOfAny(['/', '\\']);

            return index >= 0 ? fullPath[index] : Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Whether the character separates two segments of a stored path.
        /// </summary>
        /// <remarks>
        /// Both separators, always, for the reason on this class - never
        /// <see cref="System.IO.Path.DirectorySeparatorChar"/>, which on Linux excludes <c>\</c> and so
        /// stops recognising the boundaries of a path some Windows machine indexed. This is the one
        /// definition; the indexer used to carry a host-dependent copy under the same name.
        /// </remarks>
        public static bool IsSeparator(char value)
        {
            return value is '/' or '\\';
        }

        /// <summary>
        /// Whether a path is a folder itself or lies anywhere beneath it, compared ordinally.
        /// </summary>
        /// <remarks>
        /// A boundary check rather than <c>StartsWith(folder + separator)</c>, which misses everything under
        /// a root such as <c>/</c> or <c>D:\</c>: those keep their trailing separator through
        /// <see cref="System.IO.Path.TrimEndingDirectorySeparator(string)"/>, so the appended one doubled it.
        /// Either separator ends a segment, for the reason on this class.
        /// </remarks>
        public static bool IsUnder(string fullPath, string folder)
        {
            ArgumentNullException.ThrowIfNull(fullPath);
            ArgumentNullException.ThrowIfNull(folder);

            return fullPath.StartsWith(folder, StringComparison.Ordinal)
                && (fullPath.Length == folder.Length
                    || (folder.Length > 0 && IsSeparator(folder[^1]))
                    || IsSeparator(fullPath[folder.Length]));
        }
    }
}
