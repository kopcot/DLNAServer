namespace DlnaServer.Persistence.Entities
{
    /// <summary>
    /// Video characteristics of a media file.
    /// </summary>
    internal sealed class VideoStreamEntity : EntityBase
    {
        public int MediaFileId { get; set; }

        public MediaFileEntity? MediaFile { get; set; }

        public TimeSpan? Duration { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        public double? FrameRate { get; set; }

        /// <summary>
        /// Display aspect ratio as reported by the container, for example <c>16:9</c>.
        /// </summary>
        public string? AspectRatio { get; set; }

        public long? Bitrate { get; set; }

        public string? PixelFormat { get; set; }

        /// <summary>
        /// Rotation in degrees from the container metadata. Phone footage relies on this to display upright.
        /// </summary>
        public int? Rotation { get; set; }

        public string? Codec { get; set; }
    }
}
