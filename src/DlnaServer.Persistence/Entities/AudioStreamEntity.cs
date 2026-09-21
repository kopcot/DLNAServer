namespace DlnaServer.Persistence.Entities
{
    /// <summary>
    /// One audio track of a media file, whether it is an audio file or a track of a video.
    /// </summary>
    internal sealed class AudioStreamEntity : EntityBase
    {
        public int MediaFileId { get; set; }

        public MediaFileEntity? MediaFile { get; set; }

        /// <summary>
        /// Position of the track within the container, preserving the order a renderer expects.
        /// </summary>
        public int StreamIndex { get; set; }

        /// <summary>
        /// The track's own name, where the container carries one - typically what names a dub.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Whether the container marks this track as the one to play when the renderer is not told.
        /// </summary>
        public bool IsDefault { get; set; }

        public TimeSpan? Duration { get; set; }

        public string? Codec { get; set; }

        public long? Bitrate { get; set; }

        public int? SampleRate { get; set; }

        public int? Channels { get; set; }

        public string? Language { get; set; }
    }
}
