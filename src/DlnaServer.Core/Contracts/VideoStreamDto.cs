namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// Video characteristics of a media file.
    /// </summary>
    public sealed record VideoStreamDto
    {
        public TimeSpan? Duration { get; init; }

        public int? Width { get; init; }

        public int? Height { get; init; }

        public double? FrameRate { get; init; }

        /// <summary>
        /// Display aspect ratio as reported by the container, for example <c>16:9</c>.
        /// </summary>
        public string? AspectRatio { get; init; }

        public long? Bitrate { get; init; }

        public string? PixelFormat { get; init; }

        /// <summary>
        /// Rotation in degrees from container metadata. Phone footage relies on this to display upright.
        /// </summary>
        public int? Rotation { get; init; }

        public string? Codec { get; init; }
    }
}
