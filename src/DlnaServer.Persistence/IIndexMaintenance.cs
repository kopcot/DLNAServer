namespace DlnaServer.Persistence
{
    /// <summary>
    /// Empties the media index without touching the database file.
    /// </summary>
    /// <remarks>
    /// Here rather than on a repository because emptying spans every table the index owns, and no single
    /// repository is the right owner of "all of it". Public, like <see cref="IDatabaseInitializer"/>, so
    /// the admin project can reach it while <c>DlnaDbContext</c> and the entities stay internal.
    /// <para>
    /// The index is derived data - a scan rebuilds it from the source folders - which is what makes this
    /// a maintenance action rather than a data loss. What it does cost is the metadata and thumbnail
    /// records: ffprobe has to run again over the whole library, though thumbnails already written beside
    /// the media are adopted rather than regenerated.
    /// </para>
    /// </remarks>
    public interface IIndexMaintenance
    {
        /// <summary>
        /// Deletes every indexed directory and file, and hands the freed pages back to the filesystem.
        /// </summary>
        Task<IndexClearResult> ClearIndexAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes the query planner's statistics for the index as it now stands.
        /// </summary>
        /// <remarks>
        /// <c>PRAGMA optimize</c>, which is what makes <c>PRAGMA analysis_limit=800</c> in
        /// <c>SqlitePragmaInterceptor</c> mean anything. <c>analysis_limit</c> only bounds a later
        /// <c>ANALYZE</c>, and nothing ever ran one - so <c>sqlite_stat1</c> was empty and every plan in
        /// this server, including the correlated <c>EXISTS</c> in the exclusion filter and the
        /// multi-index Browse predicates, was chosen on default heuristics.
        /// <para>
        /// Call it after a pass that changed the index, not on a schedule: it is cheap when the
        /// statistics are still current and pointless when nothing moved.
        /// </para>
        /// </remarks>
        Task OptimizeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gives back the memory SQLite is holding for connections that are not currently in use.
        /// </summary>
        /// <remarks>
        /// Every connection carries its own page cache, sized by <c>Database.CacheSizeInMegabytes</c>,
        /// and a pooled connection keeps that cache for as long as it sits in the pool - returning one
        /// releases nothing, and the pool prunes only after a connection has gone unused across two of
        /// its own 120-240 second ticks. On a NAS that is the largest single block of memory the server
        /// holds outside the managed heap, and nothing in normal operation reclaims it.
        /// <para>
        /// Freeing it here hands it to the C allocator, which is not the same as handing it to the
        /// operating system - see <c>NativeHeapTrimmer</c> in the host, which is the second half.
        /// Costs the next few queries a cold cache and nothing else.
        /// </para>
        /// </remarks>
        Task ReleaseMemoryAsync(CancellationToken cancellationToken = default);
    }
}
