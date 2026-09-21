namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// One audio track of a media file, whether it is an audio file or a track of a video.
    /// </summary>
    public sealed record AudioStreamDto
    {
        /// <summary>
        /// Position of the track within the container, preserving the order a renderer expects.
        /// </summary>
        public required int StreamIndex { get; init; }

        /// <summary>
        /// The track's own name, where the container carries one - typically what names a dub.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        /// Whether the container marks this track as the one to play when the renderer is not told.
        /// </summary>
        public bool IsDefault { get; init; }

        public TimeSpan? Duration { get; init; }

        public string? Codec { get; init; }

        public long? Bitrate { get; init; }

        public int? SampleRate { get; init; }

        public int? Channels { get; init; }

        public string? Language { get; init; }
    }
}
