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
        /// The folder everything was written into - empty when the upload was refused before one was known.
        /// </summary>
        public required string Destination { get; init; }

        public required IReadOnlyList<UploadedFile> Files { get; init; }

        /// <summary>
        /// Why the upload as a whole was refused, or null when it was read to the end.
        /// </summary>
        /// <remarks>
        /// A refused upload is still reported, so the page that sent it can say why instead of the browser
        /// landing on a bare error. <see cref="Files"/> then holds whatever was decided before the refusal.
        /// </remarks>
        public string? Refusal { get; init; }
    }
}
