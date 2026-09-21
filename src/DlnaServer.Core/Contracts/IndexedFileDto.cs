namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// The minimum needed to reconcile one indexed file against the filesystem.
    /// </summary>
    /// <remarks>
    /// Deliberately narrow. Reconciliation walks the entire index, so pulling full rows would load a
    /// 20,000-file library into memory for a pass that only needs a path and a stamp.
    /// </remarks>
    public sealed record IndexedFileDto
    {
        public required Guid PublicId { get; init; }

        public required string FullPath { get; init; }

        /// <summary>
        /// Stamp recorded at the last scan, compared against the file on disk to spot a change.
        /// </summary>
        public required string ContentStamp { get; init; }
    }
}
