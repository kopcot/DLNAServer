namespace DlnaServer.Core.Delivery
{
    /// <summary>
    /// Holds the bytes of files already served, so a file is read from the disc once.
    /// </summary>
    /// <remarks>
    /// The purpose is acoustic rather than throughput. The NAS drives are mechanical and audible in the
    /// room, so waking a spun-down disc to re-read a file the server has already sent is noise. Memory
    /// held here buys silence, deliberately - see <c>docs/history.md</c> section 6, M6.
    /// <para>
    /// The cache owns its own store with its own byte budget, separate from any other memory cache in
    /// the process, so a file payload can never evict something unrelated and vice versa.
    /// </para>
    /// </remarks>
    public interface IServedFileCache
    {
        /// <summary>
        /// False when caching is switched off in configuration, in which case every request reads the disc.
        /// </summary>
        /// <remarks>
        /// Reading this after the switch has been turned off also <b>releases</b> whatever was cached.
        /// Switching the cache off is how an operator reclaims this memory, so holding the payloads until
        /// their own expiry would defeat the only reason to touch the setting.
        /// </remarks>
        bool IsEnabled { get; }

        /// <summary>
        /// The byte budget actually in force, after the machine-relative clamp.
        /// </summary>
        /// <remarks>
        /// Exposed because the configured value is not the operative one: it is clamped to
        /// <see cref="Core.Configuration.FileCacheOptions.AvailableMemoryDivisor"/> of the machine's
        /// memory, so on a 2 GB box a generous setting silently resolves to a fraction of itself. Fixed
        /// for the life of the store.
        /// </remarks>
        long BudgetInBytes { get; }

        /// <summary>
        /// The largest payload that will be accepted, after clamping to a share of
        /// <see cref="BudgetInBytes"/>.
        /// </summary>
        long MaxFileSizeInBytes { get; }

        /// <summary>
        /// Returns the cached bytes of a file, refreshing its sliding lifetime.
        /// </summary>
        /// <remarks>
        /// Synchronous and allocation-free on a hit, because it sits directly on the request path: a
        /// renderer streaming a film issues one of these per range request. The path alone identifies a
        /// payload - the content class decides only retention, which is settled when it is stored.
        /// <para>
        /// Payloads are <see cref="ReadOnlyMemory{T}"/> rather than arrays so a response can stream one
        /// through <c>AsStream()</c> without copying it, and so the cache is free to hold a slice of a
        /// larger buffer later without changing any caller.
        /// </para>
        /// </remarks>
        bool TryGet(string filePath, out ReadOnlyMemory<byte> content);

        /// <summary>
        /// Reads a file into the cache and returns its bytes, or empty when it cannot be cached.
        /// </summary>
        /// <remarks>
        /// An empty result means "not cached, serve it from the disc" - the file is missing, empty,
        /// larger than <see cref="Core.Configuration.FileCacheOptions.MaxFileSizeInMegabytes"/>,
        /// unreadable, or caching is off. It is never an error the caller has to handle. Zero-length
        /// files are rejected before being stored, so empty is unambiguous.
        /// <para>
        /// Concurrent callers for the same path share one read: the first starts it and the rest await
        /// the same operation, so N simultaneous requests for one thumbnail cost one disc read and one
        /// allocation rather than N of each.
        /// </para>
        /// </remarks>
        Task<ReadOnlyMemory<byte>> LoadAsync(
            string filePath,
            CachedContentClass contentClass,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reports how much is held and how it is being used, as counters only.
        /// </summary>
        /// <remarks>
        /// Cheap enough for a page that refreshes itself every few seconds: it allocates one small record
        /// and nothing proportional to the number of entries. The paths are <see cref="ListPaths"/>, which
        /// is not - at 24,000 entries a sorted copy of the keys is half a megabyte on the large object heap.
        /// </remarks>
        ServedFileCacheReport Describe();

        /// <summary>
        /// Every cached path, in ordinal order.
        /// </summary>
        /// <remarks>
        /// Mirrors the reference's <c>/Manage/memoryCache</c>, which lists its keys for the same reason:
        /// a byte total says how much is held, the paths say <b>what</b>, and only the second tells an
        /// operator whether the cache is holding the films they are watching or a folder of thumbnails.
        /// Ordered, so two calls are comparable rather than arriving in hash order.
        /// </remarks>
        IReadOnlyList<string> ListPaths();

        /// <summary>
        /// Drops every cached payload and returns how many entries went.
        /// </summary>
        /// <remarks>
        /// The memory is not necessarily returned to the operating system by the time this returns:
        /// payloads above 85 KB live on the large object heap, which is reclaimed on a gen2 collection
        /// and not compacted by default. Clearing makes them collectable, nothing more.
        /// </remarks>
        int Clear();

        /// <summary>
        /// Drops one path's payload, returning whether there was one.
        /// </summary>
        /// <remarks>
        /// For content that has been rewritten in place. The cache is keyed by path and knows nothing
        /// about content, so a regenerated thumbnail or a re-encoded film kept being served from memory
        /// until its own expiry - the path was the same, only the bytes had changed. Thumbnail paths are
        /// deterministic and the sliding window is refreshed by every browse, so in a folder being looked
        /// at the stale image was served indefinitely while the UI reported the rebuild as done.
        /// <para>
        /// Deletion is deliberately NOT a reason to evict. A renderer mid-stream must not be cut off
        /// because the file went away, which <c>ServedFileCacheTest</c> pins.
        /// </para>
        /// </remarks>
        bool Evict(string filePath);

        /// <summary>
        /// Stores bytes already in hand, for content whose source is not the file itself.
        /// </summary>
        /// <remarks>
        /// Used for a thumbnail read out of the database: the payload is keyed by the path of the file
        /// copy, so a later request hits memory whichever of the two sources filled it.
        /// </remarks>
        void Store(string filePath, CachedContentClass contentClass, ReadOnlyMemory<byte> content);
    }
}
