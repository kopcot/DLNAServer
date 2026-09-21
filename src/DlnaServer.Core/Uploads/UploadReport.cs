namespace DlnaServer.Core.Uploads
{
    /// <summary>
    /// What one upload did, file by file.
    /// </summary>
    /// <remarks>
    /// It says what was <i>written</i>, never what was indexed: the scan that follows an upload is asked
    /// for and not awaited, because a pass over a large library takes minutes.
    /// </remarks>
    public sealed record UploadReport
    {
        public required Guid Id { get; init; }

        public required DateTimeOffset CompletedUtc { get; init; }

        /// <summary>
        /// The folder everything was written into.
        /// </summary>
        public required string Destination { get; init; }

        public required IReadOnlyList<UploadedFile> Files { get; init; }
    }
}
