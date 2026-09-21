using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// Everything needed to index a newly discovered file.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="MediaFileDto"/> because a caller cannot supply an <c>Id</c> or the
    /// audit timestamps - those belong to the database.
    /// </remarks>
    public sealed record MediaFileCreateDto
    {
        public required string FullPath { get; init; }

        public required string FileName { get; init; }

        public required string Title { get; init; }

        public required string Extension { get; init; }

        /// <summary>
        /// External identifier of the parent directory, resolved to the internal key on insert.
        /// </summary>
        public Guid? DirectoryPublicId { get; init; }

        public required DlnaMime Mime { get; init; }

        public string? DlnaProfileName { get; init; }

        public required DlnaItemClass UpnpClass { get; init; }

        public required long SizeInBytes { get; init; }

        public required DateTime FileCreatedUtc { get; init; }

        /// <summary>
        /// Overrides the date the row records as its own indexing time. Null lets the store use now.
        /// </summary>
        /// <remarks>
        /// Optional and nullable rather than required, so no existing construction site changes - a
        /// <c>required</c> member on a DTO breaks every one of them (<c>PLAN.md</c> section 7b).
        /// <para>
        /// Set only for the first fill of an empty index, and only by the indexer. Recently added orders
        /// by this column, and a bulk fill gives every row the same value, which is no ordering at all:
        /// recreating the database therefore destroyed the operator's Recently added list. On a first
        /// fill the filesystem's own date is the only information available, so it is used instead. A
        /// file arriving later is genuinely new and takes the current time, which is what makes Recently
        /// added mean anything afterwards.
        /// </para>
        /// <para>
        /// <b><c>default(DateTime)</c> is reserved and must never be written here.</b>
        /// <c>DlnaDbContext.StampTimestamps</c> fills <c>CreatedUtc</c> only when it is still
        /// <c>default</c> - which is exactly what lets an explicit value survive the save - so a null and
        /// an explicit <c>DateTime.MinValue</c> collapse into the same thing, and the row would silently
        /// take the clock instead. Not reachable today: the value comes from
        /// <c>ScannedFile.FileSystemCreatedUtc</c>, which comes from
        /// <c>LibraryScanner.ResolveFileSystemDate</c>, which falls back to the write time for any
        /// implausible year and so cannot return <c>default</c>. Written down because that chain spans
        /// three files and the invariant is invisible from any one of them.
        /// </para>
        /// </remarks>
        public DateTime? IndexedUtc { get; init; }

        public required DateTime FileModifiedUtc { get; init; }

        /// <summary>
        /// Content identity derived from size and modification time, used later to detect a changed file.
        /// </summary>
        public required string ContentStamp { get; init; }
    }
}
