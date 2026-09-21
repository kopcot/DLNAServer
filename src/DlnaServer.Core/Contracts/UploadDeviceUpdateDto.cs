namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// What is known about the device that has just uploaded, and where it sent the files.
    /// </summary>
    public sealed record UploadDeviceUpdateDto
    {
        /// <summary>
        /// Identifies the device and browser. The same device must produce the same value every time, or
        /// its remembered destination is lost on each visit.
        /// </summary>
        public required string Fingerprint { get; init; }

        public required string RemoteAddress { get; init; }

        public required string UserAgent { get; init; }

        public string? AcceptLanguage { get; init; }

        /// <summary>
        /// The full path the files were written into.
        /// </summary>
        public required string Destination { get; init; }

        /// <summary>
        /// How many files this upload wrote, which is added to the device's running total.
        /// </summary>
        public required int FileCount { get; init; }
    }
}
