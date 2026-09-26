namespace DlnaServer.Core.Subtitles
{
    /// <summary>
    /// Checks a subtitle path the operator typed against the rule in <see cref="SubtitlePath"/> and against
    /// the disc, so the file's page can say whether it will work before it is added.
    /// </summary>
    /// <remarks>
    /// An abstraction because the admin UI reaches neither the filesystem nor the host's knowledge of which
    /// folders are hidden; the host implements it, the way it implements the source-folder check.
    /// </remarks>
    public interface ISubtitleFileChecker
    {
        /// <summary>
        /// True when <paramref name="input"/> names an existing subtitle file the rule allows.
        /// </summary>
        /// <param name="mediaDirectory">The media file's folder, as an absolute path.</param>
        /// <param name="input">What the operator typed.</param>
        /// <param name="relativePath">The normalised path to store.</param>
        /// <param name="problem">Why it cannot be used.</param>
        bool TryCheck(string mediaDirectory, string? input, out string relativePath, out string problem);
    }
}
