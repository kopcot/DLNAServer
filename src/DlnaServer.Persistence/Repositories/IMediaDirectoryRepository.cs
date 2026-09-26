using DlnaServer.Core.Contracts;

namespace DlnaServer.Persistence.Repositories
{
    /// <summary>
    /// Reads and writes indexed directories. The only way in or out of the directory table.
    /// </summary>
    /// <remarks>
    /// Identifiers crossing this boundary are always the external <c>PublicId</c>; the integer keys the
    /// database joins on stay inside this assembly.
    /// <para>
    /// Every <b>listing</b> method - source roots, children, search - answers with what a renderer would
    /// be shown, and there is no flag to ask for more: the admin UI and a television are served by these
    /// same methods, so the two cannot disagree about what the library holds. Folders under
    /// <c>Library.ExcludeFolders</c> are hidden, and so are folders whose subtree holds no visible file.
    /// Lookup by identifier or by path is deliberately unfiltered - the indexer reads through
    /// <see cref="GetByPathAsync"/> and <see cref="GetIndexedPageAsync"/>, and filtering those would make
    /// the next scan re-insert every hidden path and violate the unique index on the path column.
    /// </para>
    /// </remarks>
    public interface IMediaDirectoryRepository
    {
        Task<MediaDirectoryDto?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);

        Task<MediaDirectoryDto?> GetByPathAsync(string fullPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// The configured source folders, which form the top level of the DLNA root container.
        /// </summary>
        Task<IReadOnlyList<MediaDirectoryDto>> GetSourceRootsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Directories directly inside a parent, ordered by name.
        /// </summary>
        Task<int> CountChildrenAsync(
            Guid parentPublicId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MediaDirectoryDto>> GetChildrenPageAsync(
            Guid parentPublicId,
            int skip,
            int take,
            bool sortByDate,
            bool descending,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The indexed directories among the supplied paths, keyed by path.
        /// </summary>
        /// <remarks>
        /// This replaced a <c>GetExistingPathsAsync</c> that answered only "which of these exist", after
        /// which the indexer followed up with a <see cref="GetByPathAsync"/> per path, twice - roughly
        /// 2,110 round trips per pass on the live library. The rows come back from the same single
        /// query now, and that predecessor is gone: it had no caller left in <c>src</c> or in the tests.
        /// <para>
        /// Deliberately <b>not</b> filtered by <c>ExcludeFolders</c>. The indexer's own reads decide what
        /// to insert and what to relink, so hiding a row from them makes the next scan re-insert a path
        /// that is already there and violate the unique index.
        /// </para>
        /// </remarks>
        Task<IReadOnlyDictionary<string, MediaDirectoryDto>> GetExistingByPathAsync(
            IReadOnlyCollection<string> fullPaths,
            CancellationToken cancellationToken = default);


        /// <summary>
        /// A page of indexed directories ordered by path, for reconciliation against the filesystem.
        /// </summary>
        Task<IReadOnlyList<IndexedDirectoryDto>> GetIndexedPageAsync(
            string? afterFullPath,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// A page of indexed directories that have no files and no subdirectories indexed under them,
        /// ordered by path.
        /// </summary>
        /// <remarks>
        /// The unit reconciliation prunes with. A folder that leads to no media has to go, but deleting an
        /// <i>ancestor</i> of one cascades - and that cascade is what would carry away a subtree
        /// <c>ExcludeFolders</c> was only meant to hide. Removing leaves and letting the caller repeat
        /// collapses an empty chain a level at a time and can never reach past something that is still
        /// indexed.
        /// </remarks>
        Task<IReadOnlyList<IndexedDirectoryDto>> GetEmptyLeafPageAsync(
            string? afterFullPath,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds indexed directories matching every filter that is set.
        /// </summary>
        /// <remarks>
        /// Both matches are case-insensitive: the name match reads the final path segment, the path match
        /// the whole path, so an ancestor's name finds everything beneath it. Ordered by path, and bounded
        /// by <see cref="MediaDirectorySearchRequest.Take"/>.
        /// </remarks>
        Task<IReadOnlyList<MediaDirectoryDto>> SearchAsync(
            MediaDirectorySearchRequest request,
            CancellationToken cancellationToken = default);

        Task<int> CountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts directories and saves. Returns the stored rows, including their assigned identifiers.
        /// </summary>
        Task<IReadOnlyList<MediaDirectoryDto>> AddRangeAsync(
            IReadOnlyCollection<MediaDirectoryCreateDto> directories,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Re-points an indexed directory at a new parent and sets whether it is a source root.
        /// </summary>
        /// <remarks>
        /// Needed because both facts come from configuration rather than from the filesystem, and
        /// configuration changes: a folder that was a child becomes a source root the moment it is
        /// configured as one, and it is already indexed, so nothing on the insert path would ever
        /// reconsider it. Depth is not touched - it counts separators in the path, which re-rooting
        /// does not change.
        /// </remarks>
        Task<bool> SetHierarchyAsync(
            Guid publicId,
            Guid? parentPublicId,
            bool isSourceRoot,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes directories, cascading to their subdirectories and files.
        /// </summary>
        Task<int> RemoveByPublicIdsAsync(
            IReadOnlyCollection<Guid> publicIds,
            CancellationToken cancellationToken = default);
    }
}
