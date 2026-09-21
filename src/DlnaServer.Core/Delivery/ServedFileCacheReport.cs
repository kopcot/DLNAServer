namespace DlnaServer.Core.Delivery
{
    /// <summary>
    /// What the served-bytes cache is holding, for the management endpoint.
    /// </summary>
    /// <remarks>
    /// Exists because the cache's contents were previously answerable only by inference. A working set
    /// of 2991 MB against a 974 MB large object heap could have been the cache doing its job or a leak,
    /// and nothing in the process could tell the two apart - <see cref="BytesHeld"/> against
    /// <see cref="BudgetInBytes"/> settles it directly.
    /// </remarks>
    public sealed record ServedFileCacheReport
    {
        public required bool IsEnabled { get; init; }

        /// <summary>
        /// The budget in force after the machine-relative clamp, which is not always the configured one.
        /// </summary>
        public required long BudgetInBytes { get; init; }

        public required long MaxFileSizeInBytes { get; init; }

        public required int EntryCount { get; init; }

        /// <summary>
        /// Bytes currently held, as the cache's own size accounting sees them.
        /// </summary>
        public required long BytesHeld { get; init; }

        public required long Hits { get; init; }

        public required long Misses { get; init; }

        /// <summary>
        /// Payloads that missed memory and were supplied from the database rather than read from disc.
        /// </summary>
        /// <remarks>
        /// A thumbnail is stored in SQLite as well as beside the media whenever
        /// <see cref="Core.Configuration.ThumbnailOptions.StoreInDatabase"/> is on, which is the default,
        /// and every path that serves one prefers that copy. Without this figure those reads are
        /// indistinguishable from disc reads, because a payload taken from the database still misses the
        /// memory cache on its way past.
        /// <para>
        /// So the disc figure an operator wants is <see cref="Misses"/> minus this. That subtraction is
        /// an <b>upper bound</b> rather than an exact count: a serve that finds neither memory nor the
        /// database probes the cache twice on its way to the disc - once directly and once inside
        /// <see cref="IServedFileCache.LoadAsync"/> - so one such read registers two misses.
        /// </para>
        /// </remarks>
        public required long DatabaseHits { get; init; }

        /// <summary>
        /// Reads being shared between concurrent callers at this instant.
        /// </summary>
        public required int ReadsInFlight { get; init; }

        /// <summary>
        /// Every cached path, which is what makes the holding visible rather than a total.
        /// </summary>
        public required IReadOnlyList<string> Paths { get; init; }
    }
}
