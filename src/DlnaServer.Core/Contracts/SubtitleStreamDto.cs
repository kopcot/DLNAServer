namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// One subtitle track belonging to a media file.
    /// </summary>
    public sealed record SubtitleStreamDto
    {
        /// <summary>
        /// Position of the track within the container, preserving the order a renderer expects.
        /// </summary>
        public required int StreamIndex { get; init; }

        public string? Language { get; init; }

        public string? Codec { get; init; }

        /// <summary>
        /// Path to an external subtitle file, when the track comes from a sidecar rather than the container.
        /// </summary>
        public string? ExternalFilePath { get; init; }
    }
}
