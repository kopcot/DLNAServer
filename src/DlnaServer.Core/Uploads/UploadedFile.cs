namespace DlnaServer.Core.Uploads
{
    /// <summary>
    /// One file's line in an upload report.
    /// </summary>
    public sealed record UploadedFile
    {
        /// <summary>
        /// The name as the browser sent it, after the path a browser sometimes prefixes has been stripped.
        /// </summary>
        public required string FileName { get; init; }

        /// <summary>
        /// Bytes written, which is zero for anything refused before it was read.
        /// </summary>
        public required long SizeInBytes { get; init; }

        public required UploadOutcome Outcome { get; init; }

        /// <summary>
        /// Why it was refused, for a failure or a skip. Null when it was written.
        /// </summary>
        public string? Reason { get; init; }
    }
}
