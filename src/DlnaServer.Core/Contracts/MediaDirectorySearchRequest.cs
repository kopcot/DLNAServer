namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// What to look for when searching the indexed directories.
    /// </summary>
    /// <remarks>
    /// Every filter is optional and they combine with AND. All unset means "everything", bounded by
    /// <see cref="Take"/> - which is why that has a value rather than being nullable: a search with no
    /// criteria must not try to return every directory in the library.
    /// </remarks>
    public sealed record MediaDirectorySearchRequest
    {
        /// <summary>
        /// Matches anywhere in the directory's own name - its final path segment - ignoring case.
        /// </summary>
        /// <remarks>
        /// Case folding is the database's, which for SQLite's <c>LIKE</c> covers ASCII only - so
        /// <c>films</c> finds <c>Films</c>, but an accented capital is not folded to its lower-case
        /// form. Worth knowing on a library with non-English folder names.
        /// </remarks>
        public string? NameContains { get; init; }

        /// <summary>
        /// Matches anywhere in the full path, ignoring case, which is what finds a folder by an ancestor
        /// rather than by its own name.
        /// </summary>
        public string? PathContains { get; init; }

        /// <summary>
        /// Most results to return.
        /// </summary>
        public int Take { get; init; } = 200;
    }
}
