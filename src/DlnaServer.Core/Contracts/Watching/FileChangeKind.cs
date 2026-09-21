namespace DlnaServer.Core.Contracts.Watching
{
    /// <summary>
    /// What happened to a watched path.
    /// </summary>
    public enum FileChangeKind
    {
        /// <summary>
        /// The path appeared, or its contents were written to.
        /// </summary>
        CreatedOrChanged = 1,

        /// <summary>
        /// The path was renamed. <c>OldFullPath</c> carries where it came from.
        /// </summary>
        Renamed = 2,

        /// <summary>
        /// The path disappeared.
        /// </summary>
        Deleted = 3,
    }
}
