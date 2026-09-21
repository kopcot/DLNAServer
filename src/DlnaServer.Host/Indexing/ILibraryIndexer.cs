namespace DlnaServer.Host.Indexing
{
    /// <summary>
    /// Brings the media index into line with what is on disk.
    /// </summary>
    public interface ILibraryIndexer
    {
        /// <summary>
        /// Scans the configured source folders and adds anything not already indexed.
        /// </summary>
        Task<LibraryIndexResult> IndexAsync(CancellationToken cancellationToken = default);
    }
}
