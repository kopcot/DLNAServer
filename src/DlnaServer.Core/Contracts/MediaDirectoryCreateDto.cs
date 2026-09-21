namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// Everything needed to index a newly discovered directory.
    /// </summary>
    public sealed record MediaDirectoryCreateDto
    {
        public required string FullPath { get; init; }

        public required string Name { get; init; }

        /// <summary>
        /// External identifier of the parent directory, resolved to the internal key on insert.
        /// </summary>
        public Guid? ParentDirectoryPublicId { get; init; }

        public required int Depth { get; init; }

        public required bool IsSourceRoot { get; init; }

        /// <summary>
        /// Overrides the date the row records as its own indexing time. Null lets the store use now.
        /// </summary>
        /// <remarks>
        /// Optional and nullable rather than required, so no existing construction site changes - a
        /// <c>required</c> member on a DTO breaks every one of them (<c>PLAN.md</c> section 7b).
        /// <para>
        /// The folder half of the first-fill rule, and it arrived later than the file half: without it
        /// <c>DlnaDbContext.StampTimestamps</c> filled every folder's <c>CreatedUtc</c> from one
        /// <c>nowUtc</c> computed once per <c>SaveChanges</c>, so a 500-row batch shared a single
        /// timestamp while its files correctly took filesystem dates. Browse's date sort orders
        /// containers by that column, so a bulk import left folder ordering as insertion order. Set only
        /// for the first fill of a source folder, and only by the indexer.
        /// </para>
        /// <para>
        /// <b><c>default(DateTime)</c> is reserved and must never be written here.</b> The store stamps
        /// <c>CreatedUtc</c> only when it is still <c>default</c> - which is exactly what lets an explicit
        /// value survive the save - so a null and an explicit <see cref="DateTime.MinValue"/> collapse
        /// into the same thing and the row would silently take the clock instead. Not reachable today:
        /// the value comes from <see cref="Files.FileSystemDate.Resolve"/>, which falls back to the write
        /// time for any implausible year and so cannot return <c>default</c>.
        /// </para>
        /// </remarks>
        public DateTime? IndexedUtc { get; init; }
    }
}
