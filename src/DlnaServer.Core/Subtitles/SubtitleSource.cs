namespace DlnaServer.Core.Subtitles
{
    /// <summary>
    /// How a subtitle file came to be linked to a media file.
    /// </summary>
    /// <remarks>
    /// Persisted - never renumber.
    /// </remarks>
    public enum SubtitleSource
    {
        /// <summary>
        /// Found by a scan, by its name matching the media file's.
        /// </summary>
        Automatic = 1,

        /// <summary>
        /// Added by the operator on the file's page. A media file with one gets no automatic links.
        /// </summary>
        Manual = 2,

        /// <summary>
        /// An automatic link the operator removed, kept so the next scan does not add it back.
        /// </summary>
        Removed = 3,
    }
}
