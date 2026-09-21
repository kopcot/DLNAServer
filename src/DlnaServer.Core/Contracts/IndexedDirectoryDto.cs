namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// The minimum needed to reconcile one indexed directory against the filesystem.
    /// </summary>
    public sealed record IndexedDirectoryDto
    {
        public required Guid PublicId { get; init; }

        public required string FullPath { get; init; }
    }
}
