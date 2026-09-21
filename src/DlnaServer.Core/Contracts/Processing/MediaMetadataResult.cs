namespace DlnaServer.Core.Contracts.Processing
{
    /// <summary>
    /// What metadata extraction learned about one media file.
    /// </summary>
    /// <param name="AudioStreams">Every embedded audio track, in container order.</param>
    /// <param name="Video">Video track, or null when the file has none.</param>
    /// <param name="Subtitles">Every embedded subtitle track, in container order.</param>
    public sealed record MediaMetadataResult(
        IReadOnlyList<AudioStreamDto> AudioStreams,
        VideoStreamDto? Video,
        IReadOnlyList<SubtitleStreamDto> Subtitles)
    {
        /// <summary>
        /// Every tag the container carries about itself and its tracks, as name and value. Open-ended by
        /// nature - see <see cref="MediaFileTagDto"/>.
        /// </summary>
        /// <remarks>
        /// Not positional, unlike the three above, so that a caller with no tags to offer - which is
        /// every test of the typed metadata, and the reader when tag collection is switched off - does
        /// not have to say so.
        /// </remarks>
        public IReadOnlyList<MediaFileTagDto> Tags { get; init; } = [];

        /// <summary>
        /// Nothing was found. Stored anyway, so the file is not probed again on every pass.
        /// </summary>
        public static MediaMetadataResult Empty { get; } = new([], null, []);
    }
}
