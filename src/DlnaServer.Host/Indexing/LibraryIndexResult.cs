namespace DlnaServer.Host.Indexing
{
    /// <summary>
    /// What one indexing pass changed.
    /// </summary>
    /// <param name="FilesAdded">Files newly added to the index.</param>
    /// <param name="FilesUpdated">Indexed files whose bytes changed, so they will be reprocessed.</param>
    /// <param name="FilesRemoved">Indexed files that no longer exist on disk.</param>
    /// <param name="DirectoriesRemoved">Indexed directories that no longer exist on disk, or that no longer lead to any media.</param>
    /// <param name="TotalFiles">Files in the index afterwards.</param>
    /// <param name="TotalDirectories">Directories in the index afterwards.</param>
    public sealed record LibraryIndexResult(
        int FilesAdded,
        int FilesUpdated,
        int FilesRemoved,
        int DirectoriesRemoved,
        int TotalFiles,
        int TotalDirectories);
}
