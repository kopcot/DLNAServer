using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DlnaServer.Core.Configuration;
using DlnaServer.Host.Delivery;
using DlnaServer.Host.Delivery.Caching;
using DlnaServer.Host.Delivery.Prefetch;
using Microsoft.Extensions.Logging.Abstractions;
using DlnaServer.Core.Delivery;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Exercises the served-bytes cache against real files. The cache exists for an acoustic reason -
    /// a file already sent must not wake a spun-down disc again - so what matters here is that a second
    /// read never reaches the file system, and that the byte budget is honoured in real bytes.
    /// </summary>
    [TestFixture]
    internal sealed class ServedFileCacheTest
    {
        private const int BytesPerMegabyte = 1024 * 1024;

        private string _root = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Directory.CreateTempSubdirectory("dlna-cache-").FullName;
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, recursive: true);
        }

        [Test]
        public async Task LoadAsync_ThenTryGet_ServesTheSameBytesFromMemory()
        {
            // Arrange
            var path = CreateFile("media.bin", sizeInBytes: 2048);
            using var cache = CreateCache(new FileCacheOptions());

            // Act
            var loaded = await cache.LoadAsync(path, CachedContentClass.Media, CancellationToken.None);
            var isCached = cache.TryGet(path, out var cached);

            // Assert
            loaded.IsEmpty.Should().BeFalse("because the file is well within the per-file size limit");
            loaded.Length.Should().Be(2048, "because the whole file is read, not a prefix of it");
            isCached.Should().BeTrue("because a loaded file must be servable from memory afterwards");
            // Array identity, not value equality: BeSameAs cannot express this for a struct, and the
            // point of holding ReadOnlyMemory is precisely that a hit hands back a view over the same
            // buffer rather than a copy - which is what lets a response stream it through AsStream.
            MemoryMarshal.TryGetArray(cached, out var cachedSegment).Should().BeTrue(
                "because the payload is backed by an array the cache owns");
            MemoryMarshal.TryGetArray(loaded, out var loadedSegment).Should().BeTrue(
                "because the loaded payload is backed by that same array");
            cachedSegment.Array.Should().BeSameAs(loadedSegment.Array,
                "because the payload is handed out as a view rather than copied per request");
        }

        /// <summary>
        /// Deleting the file after caching it is how the test proves the second read never touches the disc.
        /// </summary>
        [Test]
        public async Task TryGet_AfterTheFileIsDeleted_StillServesTheCachedBytes()
        {
            // Arrange
            var path = CreateFile("gone.bin", sizeInBytes: 512);
            using var cache = CreateCache(new FileCacheOptions());
            _ = await cache.LoadAsync(path, CachedContentClass.Media, CancellationToken.None);

            // Act
            File.Delete(path);
            var isCached = cache.TryGet(path, out var cached);

            // Assert
            isCached.Should().BeTrue("because a cache hit must not consult the file system at all");
            cached.Length.Should().Be(512, "because the cached payload is the file as it was when read");
        }

        [Test]
        public async Task LoadAsync_ForAFileOverThePerFileLimit_DoesNotCacheIt()
        {
            // Arrange
            var path = CreateFile("big.bin", sizeInBytes: 4096);
            using var cache = CreateCache(new FileCacheOptions { MaxFileSizeInMegabytes = 0 });

            // Act
            var loaded = await cache.LoadAsync(path, CachedContentClass.Media, CancellationToken.None);

            // Assert
            loaded.IsEmpty.Should().BeTrue(
                "because a file over MaxFileSizeInMegabytes always streams from disc, by design");
            cache.TryGet(path, out _).Should().BeFalse("because nothing was stored");
        }

        /// <summary>
        /// The reference gave every entry <c>Size = 1</c>, so its byte-denominated limit was really a
        /// count of files - 1240 megabytes became 1240 payloads of any size. Entries are sized by their
        /// real length here, and this is the test that says so.
        /// </summary>
        [Test]
        public async Task LoadAsync_WithAPerFileLimitLargerThanTheBudget_ClampsItAndReadsNothing()
        {
            // Arrange
            var path = CreateFile("over-budget.bin", sizeInBytes: 2 * BytesPerMegabyte);

            using var cache = CreateCache(new FileCacheOptions
            {
                MaxTotalSizeInMegabytes = 1,
                MaxFileSizeInMegabytes = 512,
            });

            // Act
            var loaded = await cache.LoadAsync(path, CachedContentClass.Media, CancellationToken.None);

            // Assert
            loaded.IsEmpty.Should().BeTrue(
                "because the per-file limit is clamped to a share of the budget, so a 2 MB payload is "
                + "refused before it is read - the previous behaviour read the whole file and then threw "
                + "the bytes away when the store rejected them");
            cache.TryGet(path, out _).Should().BeFalse(
                "because nothing was stored");
        }

        [Test]
        public async Task LoadAsync_ForAMissingFile_ReturnsNullWithoutThrowing()
        {
            // Arrange
            using var cache = CreateCache(new FileCacheOptions());

            // Act
            var loaded = await cache.LoadAsync(
                Path.Combine(_root, "not-there.bin"),
                CachedContentClass.Media,
                CancellationToken.None);

            // Assert
            loaded.IsEmpty.Should().BeTrue(
                "because a missing file is a 404 for the caller to decide on, not an exception here");
        }

        [Test]
        public async Task LoadAsync_ForAnEmptyFile_DoesNotCacheIt()
        {
            // Arrange
            var path = CreateFile("empty.bin", sizeInBytes: 0);
            using var cache = CreateCache(new FileCacheOptions());

            // Act
            var loaded = await cache.LoadAsync(path, CachedContentClass.Media, CancellationToken.None);

            // Assert
            loaded.IsEmpty.Should().BeTrue(
                "because there is nothing to hold and a zero-length entry has no size");
        }

        [Test]
        public async Task LoadAsync_WhenCachingIsDisabled_StoresNothing()
        {
            // Arrange
            var path = CreateFile("disabled.bin", sizeInBytes: 256);
            using var cache = CreateCache(new FileCacheOptions { Enabled = false });

            // Act
            var loaded = await cache.LoadAsync(path, CachedContentClass.Media, CancellationToken.None);

            // Assert
            cache.IsEnabled.Should().BeFalse("because Enabled is the master switch");
            loaded.IsEmpty.Should().BeTrue("because with caching off every request reads the disc");
            cache.TryGet(path, out _).Should().BeFalse("because nothing was stored");
        }

        /// <summary>
        /// Switching the cache off is how an operator reclaims this memory, so the payloads must actually
        /// be released rather than held until their own expiry.
        /// </summary>
        [Test]
        public async Task IsEnabled_WhenCachingIsSwitchedOffAtRuntime_ReleasesWhatWasCached()
        {
            // Arrange
            var path = CreateFile("released.bin", sizeInBytes: 1024);
            var options = new DlnaOptions { FileCache = new FileCacheOptions() };
            var monitor = new MutableOptionsMonitor<DlnaOptions>(options);

            using var cache = new ServedFileCache(monitor, TimeProvider.System, NullLogger<ServedFileCache>.Instance);

            _ = await cache.LoadAsync(path, CachedContentClass.Media, CancellationToken.None);
            cache.TryGet(path, out _).Should().BeTrue("because the file was cached while caching was on");

            // Act
            monitor.CurrentValue = new DlnaOptions
            {
                FileCache = new FileCacheOptions { Enabled = false },
            };

            // Assert
            cache.IsEnabled.Should().BeFalse("because the monitor now reports the cache as switched off");

            monitor.CurrentValue = new DlnaOptions { FileCache = new FileCacheOptions() };
            cache.TryGet(path, out _).Should().BeFalse(
                "because switching the cache off must free the bytes, not merely stop adding to them");
        }

        [TestCase(CachedContentClass.Media)]
        [TestCase(CachedContentClass.Thumbnail)]
        [TestCase(CachedContentClass.StaticAsset)]
        public void Store_ForEveryContentClass_IsRetrievable(CachedContentClass contentClass)
        {
            // Arrange
            var path = Path.Combine(_root, $"{contentClass}.bin");
            using var cache = CreateCache(new FileCacheOptions());

            // Act
            cache.Store(path, contentClass, new byte[] { 1, 2, 3 });

            // Assert
            cache.TryGet(path, out var cached).Should().BeTrue(
                "because every content class must resolve to a retention policy, not throw");
            cached.ToArray().Should().Equal([1, 2, 3],
                "because the stored payload is served back unchanged");
        }

        /// <summary>
        /// The thumbnail endpoint stores a payload it read out of the database under the path of the file
        /// copy, so a later request hits memory whichever of the two sources filled it.
        /// </summary>
        [Test]
        public void Store_ForBytesThatNeverCameFromTheFile_IsRetrievableByThatPath()
        {
            // Arrange
            var path = Path.Combine(_root, "38", "16", "3816.jpg");
            using var cache = CreateCache(new FileCacheOptions());

            // Act
            cache.Store(path, CachedContentClass.Thumbnail, new byte[] { 9, 9 });

            // Assert
            cache.TryGet(path, out var cached).Should().BeTrue(
                "because the path is the key even when no file at that path was ever read");
            cached.ToArray().Should().Equal([9, 9], "because the database copy is what gets served");
        }

        /// <summary>
        /// The Recently-served page shows disc reads and database reads apart, and only this counter tells
        /// them apart - a payload taken from the database misses the memory cache on its way past, so
        /// without it the two are one number.
        /// </summary>
        [Test]
        public async Task Describe_CountsBytesHandedInSeparatelyFromBytesItReadItself()
        {
            // Arrange
            var fromDatabase = Path.Combine(_root, "from-database.jpg");
            var fromDisc = Path.Combine(_root, "from-disc.jpg");

            await File.WriteAllBytesAsync(fromDisc, [4, 5, 6], CancellationToken.None);

            using var cache = CreateCache(new FileCacheOptions());

            // Act
            cache.Store(fromDatabase, CachedContentClass.Thumbnail, new byte[] { 9, 9 });
            _ = await cache.LoadAsync(fromDisc, CachedContentClass.Thumbnail, CancellationToken.None);

            // Assert
            cache.Describe().DatabaseHits.Should().Be(1,
                "because exactly one payload was handed in already read, and the one the cache read from "
                + "the filesystem itself must not be counted as having come from the database");
        }

        [Test]
        public void Store_WithAnEmptyPayload_StoresNothing()
        {
            // Arrange
            var path = Path.Combine(_root, "nothing.jpg");
            using var cache = CreateCache(new FileCacheOptions());

            // Act
            cache.Store(path, CachedContentClass.Thumbnail, ReadOnlyMemory<byte>.Empty);

            // Assert
            cache.TryGet(path, out _).Should().BeFalse(
                "because an empty entry would occupy a key while serving an empty body");
        }

        /// <summary>
        /// The budget is clamped to a share of the machine, not just to the configured megabytes.
        /// </summary>
        /// <remarks>
        /// This is the defect the 2-4 GB production constraint exposed: the previous clamp was half of
        /// available memory, which permitted ~1024 MB of cache on a 2 GB box and never bound at all on
        /// the 40 GB test NAS. Asserted against the machine the test runs on rather than a fixed number,
        /// so it holds on a 2 GB server and a 40 GB workstation alike.
        /// </remarks>
        [Test]
        public void Budget_WhenTheConfiguredValueExceedsTheMachine_IsClampedToAShareOfIt()
        {
            // Arrange
            var affordable = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / FileCacheOptions.AvailableMemoryDivisor;

            // Act
            using var cache = CreateCache(new FileCacheOptions
            {
                MaxTotalSizeInMegabytes = 65_536,
                MaxFileSizeInMegabytes = 65_536,
            });

            // Assert
            cache.BudgetInBytes.Should().Be(affordable,
                "because a request for 64 GB must resolve to the machine's share rather than being granted");

            // int.MaxValue is a third clamp, below the share on any machine with more than ~4 GB. A
            // payload is held in one array and the read casts the file length to int, so a limit above
            // this could not be honoured - it overflowed to a negative length, and OverflowException is
            // not something the read catches, so it faulted every waiter and did so again on every later
            // playback of that file. On the machine this test runs on the share alone resolves well
            // above 2 GB, which is what makes the overflow reachable rather than theoretical.
            var expected = Math.Min(
                affordable / FileCacheOptions.MaxFileShareOfBudgetDivisor,
                int.MaxValue);

            cache.MaxFileSizeInBytes.Should().Be(expected,
                "because one file may never occupy the whole cache, however large the configured limit, "
                + "and never more than a single array can hold");
        }

        [Test]
        public void Budget_WhenTheConfiguredValueFitsTheMachine_IsHonouredExactly()
        {
            // Arrange
            // Act
            using var cache = CreateCache(new FileCacheOptions
            {
                MaxTotalSizeInMegabytes = 8,
                MaxFileSizeInMegabytes = 1,
            });

            // Assert
            cache.BudgetInBytes.Should().Be(8L * BytesPerMegabyte,
                "because the clamp is a ceiling, not an override - a modest setting is taken as written");
            cache.MaxFileSizeInBytes.Should().Be(1L * BytesPerMegabyte,
                "because 1 MB is below the 2 MB quarter-of-budget share, so the configured value wins");
        }

        /// <summary>
        /// Simultaneous callers for one path share a single read.
        /// </summary>
        /// <remarks>
        /// The reference achieves this with a per-path <c>SemaphoreSlim</c> and a re-check inside the
        /// lock; this proves the lock-free equivalent. Without it, every renderer browsing a folder reads
        /// the same thumbnail from the disc at the same moment - and on a 2-4 GB machine those duplicate
        /// payloads multiply the transient peak, which is what such a box cannot absorb.
        /// </remarks>
        [Test]
        public async Task LoadAsync_WhenCalledConcurrentlyForOnePath_SharesOneReadAndOneBuffer()
        {
            // Arrange
            var path = CreateFile("contended.bin", sizeInBytes: 4096);
            using var cache = CreateCache(new FileCacheOptions());

            // Act
            var callers = Enumerable
                .Range(0, 32)
                .Select(_ => Task.Run(() => cache.LoadAsync(path, CachedContentClass.Thumbnail, CancellationToken.None)))
                .ToArray();

            var payloads = await Task.WhenAll(callers);

            // Assert
            payloads.Should().OnlyContain(payload => !payload.IsEmpty,
                "because every caller gets the bytes, not just the one that happened to start the read");

            var arrays = payloads
                .Select(static payload =>
                {
                    _ = MemoryMarshal.TryGetArray(payload, out var segment);
                    return segment.Array;
                })
                .Distinct()
                .ToArray();

            arrays.Should().HaveCount(1,
                "because all 32 callers must await the same read and receive a view over one buffer - "
                + "a distinct array per caller would mean 32 disc reads and 32 allocations for one file");
        }

        /// <summary>
        /// The cache is keyed by path and knows nothing about content, so something has to tell it when
        /// the bytes behind a path have been rewritten. Thumbnail paths are deterministic and every
        /// browse refreshes the sliding window, so without this a regenerated image was served stale
        /// indefinitely in any folder someone was looking at - while the UI reported the rebuild as done.
        /// </summary>
        [Test]
        public async Task Evict_AfterTheContentIsRewritten_ServesTheNewBytes()
        {
            // Arrange
            using var cache = CreateCache(new FileCacheOptions());
            var path = CreateFile("thumb.jpg", sizeInBytes: 16);

            _ = await cache.LoadAsync(path, CachedContentClass.Thumbnail, CancellationToken.None);
            File.WriteAllBytes(path, new byte[32]);

            // Act
            var evicted = cache.Evict(path);
            var reloaded = await cache.LoadAsync(path, CachedContentClass.Thumbnail, CancellationToken.None);

            // Assert
            evicted.Should().BeTrue("because the path was held before the file was rewritten");
            reloaded.Length.Should().Be(32,
                "because the next read must see the regenerated content, not the payload cached from "
                + "before it was replaced");
        }

        [Test]
        public void Evict_ForAPathThatIsNotHeld_ReportsNothingDropped()
        {
            // Arrange
            using var cache = CreateCache(new FileCacheOptions());

            // Act
            var evicted = cache.Evict(Path.Combine(_root, "never-cached.jpg"));

            // Assert
            evicted.Should().BeFalse(
                "because callers evict on every regeneration and most files are not in the cache");
        }

        /// <summary>
        /// <see cref="Microsoft.Extensions.Caching.Memory.MemoryCache"/> removes an expired entry only when
        /// it is read or written, so a server nobody was using held every expired payload - 1.09 GB on the
        /// NAS, 12.5 hours after the last request. The sweep is what removes them now.
        /// </summary>
        [Test]
        public async Task RemoveExpired_OnACacheNobodyIsUsing_DropsOnlyTheEntriesPastTheirExpiry()
        {
            // Arrange
            var time = new MutableTimeProvider(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero));
            var monitor = new MutableOptionsMonitor<DlnaOptions>(
                new DlnaOptions { FileCache = new FileCacheOptions { ThumbnailSlidingExpirationInMinutes = 10 } });
            using var cache = new ServedFileCache(monitor, time, NullLogger<ServedFileCache>.Instance);
            var keptPath = Path.Combine(_root, "kept.jpg");

            // Both stored before the clock moves: a write after it would let the cache start its own scan.
            cache.Store(Path.Combine(_root, "expired.jpg"), CachedContentClass.Thumbnail, new byte[16]);
            monitor.CurrentValue = new DlnaOptions
            {
                FileCache = new FileCacheOptions { ThumbnailSlidingExpirationInMinutes = 60 },
            };
            cache.Store(keptPath, CachedContentClass.Thumbnail, new byte[16]);
            time.Advance(TimeSpan.FromMinutes(11));

            var heldBeforeSweep = cache.Describe().EntryCount;

            // Act
            cache.RemoveExpired();
            var isSwept = await WaitUntilAsync(() => cache.Describe().EntryCount == 1, TimeSpan.FromSeconds(10));

            // Assert
            heldBeforeSweep.Should().Be(2,
                "because nothing removes an expired entry while the cache is neither read nor written");
            isSwept.Should().BeTrue("because the sweep starts the cache's own scan for expired entries");
            cache.ListPaths().Should().Equal([keptPath],
                "because expired.jpg is a minute past its 10-minute sliding expiry and kept.jpg has 49 minutes left");
        }

        /// <summary>
        /// Switching the cache off is how an operator reclaims its memory, and only a request used to notice
        /// the switch - so on a server nobody was using, the payloads stayed until a television asked.
        /// </summary>
        [Test]
        public void RemoveExpired_AfterCachingIsSwitchedOff_EmptiesTheCache()
        {
            // Arrange
            var monitor = new MutableOptionsMonitor<DlnaOptions>(new DlnaOptions());
            using var cache = new ServedFileCache(monitor, TimeProvider.System, NullLogger<ServedFileCache>.Instance);

            cache.Store(Path.Combine(_root, "thumb.jpg"), CachedContentClass.Thumbnail, new byte[16]);
            monitor.CurrentValue = new DlnaOptions { FileCache = new FileCacheOptions { Enabled = false } };

            // Act
            cache.RemoveExpired();

            // Assert
            cache.Describe().EntryCount.Should().Be(0,
                "because a switched-off cache must give its memory back without waiting for a request");
        }

        /// <summary>
        /// The eviction callback forced a compacting collection from inside itself, while
        /// <see cref="Microsoft.Extensions.Caching.Memory.MemoryCache"/> still held the evicted entry - so the one buffer the collection was
        /// for survived it. On the NAS that left 264.9 MB on the large object heap with the cache empty.
        /// </summary>
        [Test]
        public async Task Evict_ForAFilm_TheCollectionItTriggersFreesItsBuffer()
        {
            // Arrange
            using var cache = CreateCache(new FileCacheOptions());
            var path = Path.Combine(_root, "film.mkv");

            // A collection still running for an earlier test would swallow this eviction's request.
            (await WaitUntilAsync(static () => !ServedFileCache.IsCollectingEvictedMedia, TimeSpan.FromSeconds(30)))
                .Should().BeTrue("because an earlier eviction's collection finishes within seconds");

            var buffer = StoreFilm(cache, path);

            // Act
            cache.Evict(path);
            var isFreed = await WaitUntilAsync(() => !buffer.IsAlive, TimeSpan.FromSeconds(15));

            // Assert
            isFreed.Should().BeTrue(
                "because the collection an evicted film triggers must run once the cache has let go of it, "
                + "and nothing else here forces a full collection");
        }

        private static ServedFileCache CreateCache(FileCacheOptions fileCache)
        {
            var monitor = new StaticOptionsMonitor<DlnaOptions>(new DlnaOptions { FileCache = fileCache });

            return new ServedFileCache(monitor, TimeProvider.System, NullLogger<ServedFileCache>.Instance);
        }

        // Not inlined, so no local of the caller's frame keeps the payload reachable.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference StoreFilm(ServedFileCache cache, string path)
        {
            var payload = new byte[4 * BytesPerMegabyte];

            cache.Store(path, CachedContentClass.Media, payload);

            return new WeakReference(payload);
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;

            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    return false;
                }

                await Task.Delay(50);
            }

            return true;
        }

        private string CreateFile(string name, int sizeInBytes)
        {
            var path = Path.Combine(_root, name);

            File.WriteAllBytes(path, new byte[sizeInBytes]);

            return path;
        }
    }
}
