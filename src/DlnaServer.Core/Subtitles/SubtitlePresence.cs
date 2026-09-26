namespace DlnaServer.Core.Subtitles
{
    /// <summary>
    /// Which kind of subtitles a search asks a file to carry.
    /// </summary>
    /// <remarks>
    /// <see cref="Embedded"/> and <see cref="Linked"/> ask whether the file has that kind, not whether it has
    /// only that kind: a film with both a track and a linked file matches either.
    /// </remarks>
    public enum SubtitlePresence
    {
        /// <summary>
        /// A track inside the file, or a linked subtitle or lyrics file.
        /// </summary>
        Yes = 1,

        /// <summary>
        /// A subtitle track inside the media file itself.
        /// </summary>
        Embedded = 2,

        /// <summary>
        /// A linked subtitle or lyrics file the operator has not removed.
        /// </summary>
        Linked = 3,

        /// <summary>
        /// Neither a track inside the file nor a linked file.
        /// </summary>
        No = 4,
    }
}
