namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// Looks candidate source folders up on disc and reports what each one is, without refusing anything.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="DlnaOptions"/> validation, which runs at startup and stops the server on a
    /// bad folder. This answers the same question while the operator is still typing, so every path is
    /// reported rather than the first failure thrown.
    /// </remarks>
    public interface ISourceFolderChecker
    {
        /// <summary>
        /// Checks each path in order and returns one result per path, including the usable ones.
        /// </summary>
        IReadOnlyList<SourceFolderCheck> Check(IReadOnlyCollection<string> paths);
    }
}
