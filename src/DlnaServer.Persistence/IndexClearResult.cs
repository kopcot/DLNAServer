namespace DlnaServer.Persistence
{
    /// <summary>
    /// What clearing the index removed, and whether the freed space went back to the filesystem.
    /// </summary>
    /// <param name="FilesRemoved">Indexed files deleted, with their streams and thumbnails.</param>
    /// <param name="DirectoriesRemoved">Indexed directories deleted.</param>
    /// <param name="SpaceReclaimed">
    /// Whether <c>VACUUM</c> ran. False when SQLite refused it because another connection held the
    /// database - the rows are still gone, the file just keeps its size until the next opportunity.
    /// </param>
    public sealed record IndexClearResult(
        int FilesRemoved,
        int DirectoriesRemoved,
        bool SpaceReclaimed);
}
