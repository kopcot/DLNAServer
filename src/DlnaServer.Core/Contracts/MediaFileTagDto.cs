namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// One name-and-value pair read out of a media file's own container - a tag the file carries about
    /// itself, such as artist, album or lyrics.
    /// </summary>
    /// <remarks>
    /// Deliberately untyped and open-ended. Which tags a file carries is decided by whatever wrote it,
    /// not by this server, so the alternative is a column per tag that is wrong the moment a file uses
    /// one nobody anticipated. The named columns on <see cref="MediaFileDto"/> stay where they are: those
    /// are the values the DLNA contract and the browse listings need to reason about, and these are for
    /// reading.
    /// </remarks>
    public sealed record MediaFileTagDto
    {
        /// <summary>
        /// Which track the tag came from, or null when it describes the whole file.
        /// </summary>
        public int? StreamIndex { get; init; }

        /// <summary>
        /// The tag's name as the container spells it, lower-cased by ffprobe - "artist", "album", "date".
        /// </summary>
        public required string Name { get; init; }

        public required string Value { get; init; }
    }
}
