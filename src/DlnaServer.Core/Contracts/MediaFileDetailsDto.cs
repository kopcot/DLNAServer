namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// A media file together with everything needed to render a full DIDL-Lite item.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="MediaFileDto"/> so a directory listing does not pay for metadata and
    /// thumbnail joins it never reads.
    /// </remarks>
    public sealed record MediaFileDetailsDto
    {
        public required MediaFileDto File { get; init; }

        public string? DirectoryFullPath { get; init; }

        public required IReadOnlyList<AudioStreamDto> AudioStreams { get; init; }

        public VideoStreamDto? Video { get; init; }

        public required IReadOnlyList<SubtitleStreamDto> Subtitles { get; init; }

        public ThumbnailDto? Thumbnail { get; init; }
    }
}
