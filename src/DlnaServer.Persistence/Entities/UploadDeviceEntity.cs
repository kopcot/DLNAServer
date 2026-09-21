namespace DlnaServer.Persistence.Entities
{
    /// <summary>
    /// Where a particular browser on a particular machine last sent an upload.
    /// </summary>
    /// <remarks>
    /// The upload page also remembers the destination in a cookie, which is faster and needs no query.
    /// This row is what answers when that cookie is gone - a cleared browser, a private window, a second
    /// browser on the same machine - so the operator is not asked to find the folder again.
    /// <para>
    /// It records who uploaded from where as a side effect, and that is useful when reviewing what landed
    /// in the media folders. The fuller record is <c>logs/uploadSecurity.log</c>; this table keeps only
    /// the last upload per device.
    /// </para>
    /// </remarks>
    internal sealed class UploadDeviceEntity : EntityBase
    {
        /// <summary>
        /// Identifies the device and browser, so the same one is recognised on its next visit.
        /// </summary>
        public required string Fingerprint { get; set; }

        public required string RemoteAddress { get; set; }

        public required string UserAgent { get; set; }

        /// <summary>
        /// The browser's <c>Accept-Language</c>, which is the closest thing to a language setting an
        /// upload carries.
        /// </summary>
        public string? AcceptLanguage { get; set; }

        /// <summary>
        /// The full path last uploaded into.
        /// </summary>
        public required string LastDestination { get; set; }

        public DateTime LastUploadUtc { get; set; }

        /// <summary>
        /// How many files this device has sent altogether.
        /// </summary>
        public int UploadCount { get; set; }
    }
}
