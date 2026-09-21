using DlnaServer.Core.Configuration;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Delivery;
using DlnaServer.Core.Dlna;
using DlnaServer.Host.Delivery;
using DlnaServer.Host.Delivery.Caching;
using DlnaServer.Host.Delivery.Prefetch;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// One resolver decides where a file's bytes come from, so the renderer port and the admin UI cannot
    /// treat the same film differently.
    /// </summary>
    /// <remarks>
    /// The admin controller originally read the cache without ever filling it, which meant a film watched
    /// through the admin page left the disc to be woken again while the same film watched on a television
    /// did not. These tests pin the shared behaviour rather than either caller's copy of it.
    /// </remarks>
    [TestFixture]
    internal sealed class MediaContentResolverTest
    {
        private const string FilePath = "/media/movies/Film.mkv";

        private ServedFileCache _cache = null!;
        private MediaCacheBacklog _backlog = null!;
        private MediaContentResolver _resolver = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new StaticOptionsMonitor<DlnaOptions>(new DlnaOptions());

            _cache = new ServedFileCache(options, NullLogger<ServedFileCache>.Instance);
            _backlog = new MediaCacheBacklog();
            _resolver = new MediaContentResolver(_cache, _backlog);
        }

        [TearDown]
        public void TearDown()
        {
            _cache.Dispose();
        }

        [Test]
        public void Resolve_WhenTheBytesAreHeld_ServesFromMemoryAndQueuesNothing()
        {
            // Arrange
            _cache.Store(FilePath, CachedContentClass.Media, new byte[] { 1, 2, 3 });

            // Act
            var source = _resolver.Resolve(CreateFile());

            // Assert
            source.IsCached.Should().BeTrue("because the payload is already in memory");
            source.Content.Length.Should().Be(3, "because the caller streams these bytes rather than the file");
            _backlog.Reader.TryRead(out _).Should().BeFalse(
                "because a file already cached must not be queued to be read again");
        }

        /// <summary>
        /// The queue is the whole reason a second viewing does not wake the disc.
        /// </summary>
        [Test]
        public void Resolve_WhenTheBytesAreNotHeld_ServesFromDiscAndQueuesTheRead()
        {
            // Arrange
            // Act
            var source = _resolver.Resolve(CreateFile());

            // Assert
            source.IsCached.Should().BeFalse(
                "because the first request always touches the platter - waiting for the read would add "
                + "latency and save nothing");

            _backlog.Reader.TryRead(out var queued).Should().BeTrue(
                "because the read is queued behind the response, which is what makes every later request "
                + "come from memory");
            queued.FilePath.Should().Be(FilePath, "because the queued read is for the file just served");
        }

        [Test]
        public void Resolve_ForAFileThatAlreadyFailedToCache_QueuesNothing()
        {
            // Arrange
            // Act
            var source = _resolver.Resolve(CreateFile(isExcludedFromCache: true));

            // Assert
            source.IsCached.Should().BeFalse("because nothing was ever held for it");
            _backlog.Reader.TryRead(out _).Should().BeFalse(
                "because retrying a read that already failed would repeat the failure on every request - "
                + "the flag is cleared when the file's content changes, which is what earns another attempt");
        }

        /// <remarks>
        /// <b>Reported by a customer.</b> A file over the per-file limit used to be written into the
        /// database as permanently uncacheable, and the flag is cleared only by the file's content
        /// changing - so raising <c>MaxFileSizeInMegabytes</c> the next day left the file streaming from
        /// the platter for good, with no way back short of rebuilding the index. The size is compared
        /// live instead, and nothing is recorded.
        /// </remarks>
        [Test]
        public void Resolve_ForAFileOverThePerFileLimit_QueuesNothingAndCondemnsNothing()
        {
            // Arrange
            var options = new DlnaOptions();
            options.FileCache.MaxFileSizeInMegabytes = 1;

            using var cache = new ServedFileCache(
                new StaticOptionsMonitor<DlnaOptions>(options),
                NullLogger<ServedFileCache>.Instance);

            var resolver = new MediaContentResolver(cache, _backlog);

            // Act
            var source = resolver.Resolve(CreateFile(sizeInBytes: 4 * 1024 * 1024));

            // Assert
            source.IsCached.Should().BeFalse("because a file that does not fit is never held in memory");
            _backlog.Reader.TryRead(out _).Should().BeFalse(
                "because queuing a read that the cache will refuse on size wakes the drain for nothing on "
                + "every single request");
        }

        /// <remarks>
        /// The other half of the same fix: the limit is configuration, so the same file must become
        /// cacheable the moment the operator raises it, with no rescan and nothing to clear.
        /// </remarks>
        [Test]
        public void Resolve_AfterThePerFileLimitIsRaised_QueuesTheFileThatPreviouslyDidNotFit()
        {
            // Arrange
            var options = new DlnaOptions();
            options.FileCache.MaxFileSizeInMegabytes = 1;

            var monitor = new StaticOptionsMonitor<DlnaOptions>(options);

            using var cache = new ServedFileCache(monitor, NullLogger<ServedFileCache>.Instance);
            var resolver = new MediaContentResolver(cache, _backlog);
            var file = CreateFile(sizeInBytes: 4 * 1024 * 1024);

            _ = resolver.Resolve(file);
            _backlog.Reader.TryRead(out _).Should().BeFalse("because it does not fit under the old limit");

            // Act
            options.FileCache.MaxFileSizeInMegabytes = 16;

            var source = resolver.Resolve(file);

            // Assert
            source.IsCached.Should().BeFalse("because nothing has been read into memory yet");
            _backlog.Reader.TryRead(out var queued).Should().BeTrue(
                "because raising the limit is all that should be needed to make the file cacheable again");
            queued.FilePath.Should().Be(FilePath, "because the queued read is for that same file");
        }

        private static MediaFileDto CreateFile(bool isExcludedFromCache = false, long sizeInBytes = 1024)
        {
            return new MediaFileDto
            {
                PublicId = Guid.NewGuid(),
                FullPath = FilePath,
                FileName = "Film.mkv",
                Title = "Film",
                Extension = ".mkv",
                DirectoryPublicId = Guid.NewGuid(),
                Mime = DlnaMime.VideoXMatroska,
                UpnpClass = DlnaItemClass.VideoItem,
                SizeInBytes = sizeInBytes,
                FileCreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FileModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsExcludedFromCache = isExcludedFromCache,
                ContentStamp = "1024:638000000000000000",
            };
        }
    }
}
