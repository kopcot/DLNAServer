using System.Collections.Concurrent;
using System.Runtime;
using DlnaServer.Core.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using DlnaServer.Core.Delivery;

namespace DlnaServer.Host.Delivery.Caching
{
    /// <inheritdoc cref="IServedFileCache"/>
    internal sealed partial class ServedFileCache : IServedFileCache, IDisposable
    {
        private const long BytesPerMegabyte = 1024L * 1024L;

        // Read by Store and TryGet so a read that completes after shutdown cannot touch a disposed
        // MemoryCache. Volatile rather than locked: one writer, at dispose, and staleness only costs a
        // caught ObjectDisposedException on the very last fill.
        private volatile bool _isDisposed;

        /// <summary>
        /// Smallest payload whose allocation is rounded to a bucket.
        /// </summary>
        private const int BucketFloor = 512 * 1024;

        private const int BucketSize = 64 * 1024;

        /// <summary>
        /// How often expired entries are removed while nothing is asking for anything.
        /// </summary>
        /// <remarks>
        /// <see cref="MemoryCacheOptions.ExpirationScanFrequency"/> is not a timer: <see cref="MemoryCache"/>
        /// only looks for expired entries when it is read or written. An idle server therefore held every
        /// expired payload indefinitely - 1.09 GB in 24,305 entries on the NAS, 12.5 hours after the last
        /// request, on 2026-09-26.
        /// </remarks>
        private static readonly TimeSpan _expiredSweepInterval = TimeSpan.FromMinutes(1);

        // Never stored. Removing it is the cheapest call that makes MemoryCache run its own expired-entry
        // scan, which walks the entries without allocating - Compact(0) builds three lists of every live one.
        private static readonly object _expirationScanTrigger = new();

        private readonly MemoryCache _cache;

        private readonly ITimer _expiredSweep;

        /// <summary>
        /// Reads currently in progress, keyed by path, so simultaneous callers share one.
        /// </summary>
        /// <remarks>
        /// The reference solves this with a <c>SemaphoreSlim</c> per path and a re-check of the cache
        /// inside the lock. This is the same idea without the lock: the winner's task is the shared
        /// result, so there is no acquire/release to leave unbalanced and no lock object whose lifetime
        /// has to be managed - the reference removes its semaphore from the dictionary while other
        /// callers are still waiting on it, which lets a later caller create a second one and enter
        /// concurrently.
        /// <para>
        /// <see cref="Lazy{T}"/> rather than a bare task because
        /// <see cref="ConcurrentDictionary{TKey, TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/> may invoke
        /// its factory more than once under contention; only the winner's value is published, but without
        /// the laziness a losing factory would already have started a second read.
        /// </para>
        /// </remarks>
        private readonly ConcurrentDictionary<string, Lazy<Task<ReadOnlyMemory<byte>>>> _readsInFlight =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Payloads handed in already read, which today means a thumbnail taken out of the database.
        /// </summary>
        private long _databaseFills;

        private static int _blockGCCollectInProgress;

        // Set by every film eviction, so one that lands while a collection is already running gets another.
        private static int _blockGCCollectRequested;

        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly ILogger<ServedFileCache> _logger;

        public ServedFileCache(
            IOptionsMonitor<DlnaOptions> options,
            TimeProvider timeProvider,
            ILogger<ServedFileCache> logger)
        {
            _options = options;
            _logger = logger;

            var fileCache = options.CurrentValue.FileCache;
            BudgetInBytes = ResolveBudgetInBytes(fileCache);

            _cache = new MemoryCache(new MemoryCacheOptions
            {
                // The injected clock, so a test can move past an expiry instead of waiting for it.
                Clock = new TimeProviderClock(timeProvider),
                // Half the sweep interval, so every tick is late enough for the scan it asks for to run.
                ExpirationScanFrequency = _expiredSweepInterval / 2,
                SizeLimit = BudgetInBytes,

                // Without this GetCurrentStatistics() returns null, and the hit rate is the only number
                // that says whether the memory this cache holds is buying anything.
                TrackStatistics = true,
            });

            LogBudget(
                BudgetInBytes / BytesPerMegabyte,
                fileCache.MaxTotalSizeInMegabytes,
                GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / BytesPerMegabyte);

            _expiredSweep = timeProvider.CreateTimer(
                static state => ((ServedFileCache)state!).RemoveExpired(),
                this,
                _expiredSweepInterval,
                _expiredSweepInterval);
        }

        public long BudgetInBytes { get; }

        /// <remarks>
        /// Uses the budget resolved at construction rather than recomputing it. The budget cannot change
        /// - <see cref="MemoryCacheOptions.SizeLimit"/> is fixed for the life of the store - and
        /// recomputing it would call <see cref="GC.GetGCMemoryInfo(GCKind)"/> on every store and every
        /// cache miss, which is on the request path.
        /// </remarks>
        public long MaxFileSizeInBytes => ResolveMaximumFileSizeInBytes(_options.CurrentValue.FileCache);

        public bool IsEnabled
        {
            get
            {
                var isEnabled = _options.CurrentValue.FileCache.Enabled;

                if (!isEnabled && _cache.Count > 0)
                {
                    // Switching the cache off is how an operator reclaims this memory, so holding the
                    // payloads afterwards would defeat the only reason to touch the setting.
                    _cache.Clear();
                    LogClearedAfterDisable();
                }

                return isEnabled;
            }
        }

        public bool TryGet(string filePath, out ReadOnlyMemory<byte> content)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            // _isDisposed is checked here as well as in Store, which the field's own remark already
            // claimed ("Read by Store and TryGet"). A request arriving during shutdown reached
            // MemoryCache.TryGetValue on a disposed store and got an ObjectDisposedException where the
            // honest answer is "not cached" - and the caller's fallback is simply to serve from disc.
            if (_isDisposed)
            {
                content = ReadOnlyMemory<byte>.Empty;
                return false;
            }

            if (IsEnabled && _cache.TryGetValue(filePath, out ReadOnlyMemory<byte> cached) && !cached.IsEmpty)
            {
                content = cached;
                return true;
            }

            content = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        public async Task<ReadOnlyMemory<byte>> LoadAsync(
            string filePath,
            CachedContentClass contentClass,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (!IsEnabled)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            if (TryGet(filePath, out var cached))
            {
                return cached;
            }

            var inFlight = _readsInFlight.GetOrAdd(
                filePath,
                static (path, state) => new Lazy<Task<ReadOnlyMemory<byte>>>(
                    () => state.Cache.ReadAndStoreAsync(path, state.ContentClass)),
                (Cache: this, ContentClass: contentClass));

            // Retired when the SHARED read finishes, not in a finally on each waiter. A finally here ran
            // on a waiter's cancellation too - so a renderer that cancels and retries, which is what
            // ordinary seeking looks like, removed the marker while the read was still running and the
            // retry started a second concurrent read of the same file. Attaching the removal to the task
            // means it happens exactly once, when there is genuinely nothing left to share.
            RetireWhenComplete(filePath, inFlight);

            // The shared read deliberately does not take this caller's token: cancelling one request
            // must not fail the read every other caller is waiting on. WaitAsync gives each caller
            // its own cancellation without touching the work.
            return await inFlight.Value.WaitAsync(cancellationToken);
        }

        public void Store(string filePath, CachedContentClass contentClass, ReadOnlyMemory<byte> content)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            // Counted here rather than at the three call sites that read a thumbnail out of the database
            // - the media port, the admin port and the Browse prefetch - because every caller of the
            // public method is handing over bytes it read from somewhere other than the filesystem, and a
            // fourth would otherwise have to remember to count itself. The disc path goes to StoreCore
            // and is not counted. Counted even when the store is then refused: the payload still came
            // from the database and was still served.
            if (!content.IsEmpty)
            {
                _ = Interlocked.Increment(ref _databaseFills);
            }

            StoreCore(filePath, contentClass, content);
        }

        private void StoreCore(string filePath, CachedContentClass contentClass, ReadOnlyMemory<byte> content)
        {
            if (_isDisposed)
            {
                return;
            }

            var options = _options.CurrentValue.FileCache;

            if (!IsEnabled || content.IsEmpty || content.Length > ResolveMaximumFileSizeInBytes(options))
            {
                return;
            }

            using var entry = _cache.CreateEntry(filePath);

            entry.Value = content;

            // Sized by the real payload length. The reference gave its entity caches Size = 1, which made
            // a byte-denominated limit meaningless there - its file cache sizes by length, as this does.
            entry.Size = content.Length;
            entry.SlidingExpiration = ResolveSlidingExpiration(contentClass, options);
            entry.AbsoluteExpirationRelativeToNow = ResolveAbsoluteExpiration(contentClass);
            entry.Priority = ResolvePriority(contentClass);

            if (contentClass == CachedContentClass.Media)
            {
                entry.RegisterPostEvictionCallback(OnMediaEvicted);
            }

            LogStored(filePath, content.Length, contentClass);
        }

        /// <summary>
        /// Whether the collection an evicted film asked for is still running.
        /// </summary>
        internal static bool IsCollectingEvictedMedia => Volatile.Read(ref _blockGCCollectInProgress) != 0;

        private static void OnMediaEvicted(object key, object? value, EvictionReason reason, object? state)
        {
            Volatile.Write(ref _blockGCCollectRequested, 1);

            if (Interlocked.Exchange(ref _blockGCCollectInProgress, 1) != 0)
            {
                return;
            }

            // Not collected here: MemoryCache holds the evicted entry, and so its buffer, until this callback
            // returns, so a collection inside it could never free the film that triggered it. On the NAS
            // that left 264.9 MB on the large object heap with the cache empty, until a manual release.
            _ = CollectEvictedMediaAsync();
        }

        private static async Task CollectEvictedMediaAsync()
        {
            do
            {
                Volatile.Write(ref _blockGCCollectRequested, 0);

                // Long enough for the callback that queued this to have returned, and for any other film
                // evicted by the same sweep to be released with it by one collection instead of several.
                await Task.Delay(TimeSpan.FromSeconds(1));

                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

                Volatile.Write(ref _blockGCCollectInProgress, 0);
            }
            // Again if a film was evicted meanwhile - unless its own callback has already started a run.
            while (Volatile.Read(ref _blockGCCollectRequested) != 0
                && Interlocked.Exchange(ref _blockGCCollectInProgress, 1) == 0);
        }


        public ServedFileCacheReport Describe()
        {
            var statistics = _cache.GetCurrentStatistics();

            return new ServedFileCacheReport
            {
                IsEnabled = _options.CurrentValue.FileCache.Enabled,
                BudgetInBytes = BudgetInBytes,
                MaxFileSizeInBytes = MaxFileSizeInBytes,
                EntryCount = _cache.Count,
                BytesHeld = statistics?.CurrentEstimatedSize ?? 0,
                Hits = statistics?.TotalHits ?? 0,
                Misses = statistics?.TotalMisses ?? 0,
                DatabaseHits = Interlocked.Read(ref _databaseFills),
                ReadsInFlight = _readsInFlight.Count,

                // Ordered, so two calls are comparable rather than arriving in hash order.
                Paths = _cache.Keys
                    .OfType<string>()
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
            };
        }

        public int Clear()
        {
            var cleared = _cache.Count;

            _cache.Clear();
            LogCleared(cleared);

            return cleared;
        }

        /// <summary>
        /// Removes every entry past its expiry, and nothing else.
        /// </summary>
        /// <remarks>
        /// The removal itself is done by <see cref="MemoryCache"/>'s own scan, which this starts on a
        /// thread-pool task and does not wait for. The scan runs when it last ran longer ago than
        /// <see cref="MemoryCacheOptions.ExpirationScanFrequency"/>, which is set to half the sweep interval
        /// for exactly that reason.
        /// </remarks>
        internal void RemoveExpired()
        {
            // IsEnabled empties a cache that has been switched off, which an idle server otherwise never asks.
            if (_isDisposed || !IsEnabled)
            {
                return;
            }

            try
            {
                _cache.Remove(_expirationScanTrigger);
            }
            catch (ObjectDisposedException)
            {
                // A tick that was already running when the store was disposed. An exception escaping a
                // timer callback ends the process, and there is nothing left to sweep.
            }
        }

        public bool Evict(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (_isDisposed || !_cache.TryGetValue(filePath, out _))
            {
                return false;
            }

            _cache.Remove(filePath);
            LogEvicted(filePath);

            return true;
        }

        public void Dispose()
        {
            // Set before disposing, so a shared read still in flight sees it and skips Store rather than
            // calling CreateEntry on a disposed MemoryCache - which surfaced as an ObjectDisposedException
            // swallowed as an unobserved task exception, with no log line, on every shutdown that caught
            // a fill in progress.
            _isDisposed = true;
            _expiredSweep.Dispose();
            _cache.Dispose();
        }

        // Drops the in-flight marker once the shared read has genuinely finished, whatever happened to
        // the callers waiting on it.
        private void RetireWhenComplete(string filePath, Lazy<Task<ReadOnlyMemory<byte>>> entry)
        {
            _ = entry.Value.ContinueWith(
                _ => _readsInFlight.TryRemove(
                    new KeyValuePair<string, Lazy<Task<ReadOnlyMemory<byte>>>>(filePath, entry)),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private async Task<ReadOnlyMemory<byte>> ReadAndStoreAsync(string filePath, CachedContentClass contentClass)
        {
            var maximumFileSize = ResolveMaximumFileSizeInBytes(_options.CurrentValue.FileCache);

            try
            {
                // Inside the try: constructing a FileInfo is cheap, but reading Length hits the
                // filesystem and throws on a path that has become unreachable - a share that dropped,
                // for instance - which is exactly the condition this method is meant to absorb.
                var info = new FileInfo(filePath);

                if (!info.Exists || info.Length == 0 || info.Length > maximumFileSize)
                {
                    return ReadOnlyMemory<byte>.Empty;
                }

                // CancellationToken.None: the read is shared between every caller waiting on this path,
                // so one of them going away must not abandon it for the others.
                var content = await ReadBucketedAsync(filePath, (int)info.Length);

                StoreCore(filePath, contentClass, content);

                return content;
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException or ObjectDisposedException)
            {
                // Not fatal: the request is served from the disc instead, which is the uncached path.
                //
                // ObjectDisposedException is the shutdown race the _isDisposed field documents as costing
                // "a caught ObjectDisposedException on the very last fill" - there was no such catch, so
                // a fill in flight when the host stopped faulted the shared task and every waiter on it,
                // including the request that started it.
                LogReadFailed(filePath, exception);
                return ReadOnlyMemory<byte>.Empty;
            }
        }

        /// <summary>
        /// Reads a payload into a buffer whose <b>allocated</b> size is one of a small set, and returns
        /// the exact slice of it.
        /// </summary>
        /// <remarks>
        /// The large object heap is never compacted unless something asks, and this cache is the only
        /// thing in the server that allocates above 85 KB at request rate, in sizes that vary with
        /// whatever file was played, and frees them on a sliding expiry. That is the textbook recipe for
        /// fragmentation, and it is what produced 20.7 MB of fragmented large object heap against 21.3 MB
        /// live on the NAS after 27 hours - the hole left by a 6.1 MB payload cannot take a 6.4 MB one.
        /// <para>
        /// Rounding the allocation to a bucket makes the holes interchangeable, so the next payload of a
        /// similar size reuses one instead of extending the heap. The slice handed back is the exact
        /// length, so nothing downstream sees the padding.
        /// </para>
        /// <para>
        /// <b>Not <see cref="System.Buffers.ArrayPool{T}"/>, deliberately.</b> The obvious fix is to rent
        /// and return, and it is wrong here: <c>FileServerController</c> hands the returned memory
        /// straight to the response as a stream, so the buffer outlives the cache entry that owns it. A
        /// pooled buffer returned on eviction would be handed to the next renter while a response was
        /// still writing it - silent, non-deterministic corruption of somebody else's payload. Ownership
        /// leaves this class, so pooling is off the table and plain allocation is the honest answer.
        /// </para>
        /// </remarks>
        private static async Task<ReadOnlyMemory<byte>> ReadBucketedAsync(string filePath, int length)
        {
            var buffer = new byte[ResolveBucketSize(length)];

            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 0,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var read = await stream.ReadAtLeastAsync(
                buffer.AsMemory(0, length),
                length,
                throwOnEndOfStream: false,
                CancellationToken.None);

            return buffer.AsMemory(0, read);
        }

        /// <remarks>
        /// Below the 85 KB large-object threshold the payload lives on the ordinary heap, which the
        /// collector compacts, so exact sizes cost nothing and padding would be pure waste - a thumbnail
        /// rounded to 64 KB would be several times its own size. Between there and
        /// <see cref="BucketFloor"/> the objects are large but the holes they leave are cheap, and the
        /// padding would still be a large fraction of the payload. Only above that does bucketing pay,
        /// and there 64 KB is under 13% of the smallest bucketed payload and under 1% of a typical one.
        /// </remarks>
        private static int ResolveBucketSize(int length)
        {
            if (length < BucketFloor)
            {
                return length;
            }

            // Rounds up to the next whole bucket. The addition cannot overflow: length is bounded by
            // MaxFileSizeInMegabytes, which is validated far below int.MaxValue.
            return (length + BucketSize - 1) / BucketSize * BucketSize;
        }

        /// <summary>
        /// The byte budget, clamped so the cache can never claim a share of the machine it cannot have.
        /// </summary>
        /// <remarks>
        /// Read once, at construction: <see cref="MemoryCacheOptions.SizeLimit"/> is fixed for the life
        /// of the store, so this is the one file-cache setting a change to which needs a restart.
        /// <para>
        /// The divisor is what makes this safe on the target hardware. Production machines have 2-4 GB,
        /// and the previous clamp of half the available memory allowed ~1024 MB of cache on a 2 GB box
        /// while never binding at all on the 40 GB test NAS.
        /// </para>
        /// </remarks>
        private static long ResolveBudgetInBytes(FileCacheOptions options)
        {
            var configured = options.MaxTotalSizeInMegabytes * BytesPerMegabyte;
            var affordable = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / FileCacheOptions.AvailableMemoryDivisor;

            return Math.Min(configured, affordable);
        }

        /// <summary>
        /// The largest payload the cache will accept, in bytes.
        /// </summary>
        /// <remarks>
        /// Clamped to a share of the total budget as well as the configured megabyte value, so a single
        /// file can never occupy the cache on its own - and so raising
        /// <see cref="FileCacheOptions.MaxFileSizeInMegabytes"/> above the budget is harmless rather than
        /// a way to hold one film and nothing else.
        /// </remarks>
        private long ResolveMaximumFileSizeInBytes(FileCacheOptions options)
        {
            var configured = options.MaxFileSizeInMegabytes * BytesPerMegabyte;
            var share = BudgetInBytes / FileCacheOptions.MaxFileShareOfBudgetDivisor;

            // Clamped to int.MaxValue, which is what makes the (int) cast in ReadAndStoreAsync safe and
            // ResolveBucketSize's claim about it true. [Range(0, 65_536)] megabytes is 64 GB, so on a
            // machine with enough memory for the share not to bind, a large configured value overflowed
            // the cast to a negative length - and OverflowException is not in the catch filter below, so
            // it faulted every waiter on that path and did so again on every later playback of the same
            // file. A payload is held in one array, so no larger value could ever be honoured anyway.
            return Math.Min(Math.Min(configured, share), int.MaxValue);
        }

        private static TimeSpan ResolveSlidingExpiration(CachedContentClass contentClass, FileCacheOptions options)
        {
            return contentClass switch
            {
                CachedContentClass.Media => TimeSpan.FromMinutes(options.MediaSlidingExpirationInMinutes),
                CachedContentClass.Thumbnail => TimeSpan.FromMinutes(options.ThumbnailSlidingExpirationInMinutes),
                CachedContentClass.StaticAsset => FileCacheOptions.StaticAssetSlidingExpiration,
                _ => throw new ArgumentOutOfRangeException(nameof(contentClass), contentClass, null),
            };
        }

        private static TimeSpan ResolveAbsoluteExpiration(CachedContentClass contentClass)
        {
            return contentClass switch
            {
                CachedContentClass.Media => FileCacheOptions.MediaAbsoluteExpiration,
                CachedContentClass.Thumbnail => FileCacheOptions.ThumbnailAbsoluteExpiration,
                CachedContentClass.StaticAsset => FileCacheOptions.StaticAssetAbsoluteExpiration,
                _ => throw new ArgumentOutOfRangeException(nameof(contentClass), contentClass, null),
            };
        }

        /// <summary>
        /// Which class gives way first once the budget is full.
        /// </summary>
        /// <remarks>
        /// Media last-in-first-out against everything else: one film is worth thousands of thumbnails by
        /// size, so letting a media payload push the small classes out would trade a great many cheap
        /// hits for one expensive one.
        /// </remarks>
        private static CacheItemPriority ResolvePriority(CachedContentClass contentClass)
        {
            return contentClass switch
            {
                CachedContentClass.Media => CacheItemPriority.Low,
                CachedContentClass.Thumbnail => CacheItemPriority.Normal,
                CachedContentClass.StaticAsset => CacheItemPriority.High,
                _ => throw new ArgumentOutOfRangeException(nameof(contentClass), contentClass, null),
            };
        }

        // MemoryCacheOptions.Clock takes the older ISystemClock rather than a TimeProvider.
        private sealed class TimeProviderClock : ISystemClock
        {
            private readonly TimeProvider _timeProvider;

            public TimeProviderClock(TimeProvider timeProvider)
            {
                _timeProvider = timeProvider;
            }

            public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();
        }
    }
}
