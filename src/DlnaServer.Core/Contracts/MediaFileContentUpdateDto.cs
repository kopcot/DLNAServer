namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// Records that a file's bytes changed on disk, so it is re-read and reprocessed.
    /// </summary>
    /// <remarks>
    /// Clearing the metadata and thumbnail stamps is what schedules reprocessing: the pending-work query
    /// selects rows whose stamps no longer match their content stamp. The reference had no equivalent,
    /// so a replaced file kept its original metadata and thumbnail indefinitely.
    /// </remarks>
    public sealed record MediaFileContentUpdateDto
    {
        public required Guid PublicId { get; init; }

        public required long SizeInBytes { get; init; }

        public required DateTime FileModifiedUtc { get; init; }

        public required string ContentStamp { get; init; }
    }
}
