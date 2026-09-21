namespace DlnaServer.Persistence.Entities
{
    /// <summary>
    /// One subtitle track belonging to a media file.
    /// </summary>
    internal sealed class SubtitleStreamEntity : EntityBase
    {
        public int MediaFileId { get; set; }

        public MediaFileEntity? MediaFile { get; set; }

        /// <summary>
        /// Position of the track within the container, preserving the order a renderer expects.
        /// </summary>
        public int StreamIndex { get; set; }

        public string? Language { get; set; }

        public string? Codec { get; set; }

        /// <summary>
        /// Path to an external subtitle file, when the track comes from a sidecar rather than the container.
        /// </summary>
        public string? ExternalFilePath { get; set; }
    }
}
