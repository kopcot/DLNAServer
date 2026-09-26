using DlnaServer.Core.Configuration;
using DlnaServer.Core.Delivery;
using DlnaServer.Core.Hosting;
using DlnaServer.Host.Delivery.Caching;
using DlnaServer.Host.Delivery.Prefetch;
using DlnaServer.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers where the backlog drain reads a payload from, which is not the same question for a preview
    /// as for a film.
    /// </summary>
    /// <remarks>
    /// Browse warms a folder's previews through this service, and it used to warm them from disc only -
    /// while <c>FileServerController.GetThumbnail</c> prefers the copy the database holds, which with
    /// <c>Thumbnails.StoreInDatabase</c> on is the copy that exists. The prefetch therefore woke the
    /// platter for bytes already in SQLite, which is the opposite of what it is for.
    /// </remarks>
    [TestFixture]
    internal sealed class MediaCacheFillHostedServiceTest
    {
        private static readonly TimeSpan _fillTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(10);

        private ServedFileCache _cache = null!;
        private MediaCacheBacklog _backlog = null!;

        [SetUp]
        public void SetUp()
        {
            _cache = new ServedFileCache(
                new StaticOptionsMonitor<DlnaOptions>(new DlnaOptions()),
                TimeProvider.System,
                NullLogger<ServedFileCache>.Instance);
            _backlog = new MediaCacheBacklog();
        }

        [TearDown]
        public void TearDown()
        {
            _cache.Dispose();
        }

        /// <summary>
        /// The image on disc is deliberately absent, so a fill that succeeds can only have come from the
        /// database.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ForAPreviewTheDatabaseHolds_FillsFromTheDatabaseRatherThanTheDisc()
        {
            // Arrange
            var stored = new byte[] { 1, 2, 3, 4 };
            var repository = new RecordingMediaFileRepository { ThumbnailContent = stored };
            var previewPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");

            using var service = CreateService(repository);
            await service.StartAsync(CancellationToken.None);

            // Act
            _ = _backlog.TryEnqueue(new MediaCacheRequest(
                Guid.NewGuid(),
                previewPath,
                CachedContentClass.Thumbnail));

            var isFilled = await WaitForCacheAsync(previewPath);
            await service.StopAsync(CancellationToken.None);

            // Assert
            isFilled.Should().BeTrue(
                "because no such file exists on disc, so the only copy of this preview is the one stored "
                + "in the database");

            _cache.TryGet(previewPath, out var cached).Should().BeTrue(
                "because the warmed preview must be readable under the path the serve path looks it up by");
            cached.Length.Should().Be(stored.Length,
                "because the bytes held are the stored image, not a truncated or empty placeholder");
        }

        /// <summary>
        /// <c>Thumbnails.StoreInDatabase</c> off, or a preview generated while it was: the image beside
        /// the media is then the only copy there is.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ForAPreviewTheDatabaseDoesNotHold_FallsBackToTheImageOnDisc()
        {
            // Arrange
            var onDisc = new byte[] { 9, 9, 9, 9, 9 };
            var repository = new RecordingMediaFileRepository { ThumbnailContent = null };
            var previewPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");

            await File.WriteAllBytesAsync(previewPath, onDisc, CancellationToken.None);

            try
            {
                using var service = CreateService(repository);
                await service.StartAsync(CancellationToken.None);

                // Act
                _ = _backlog.TryEnqueue(new MediaCacheRequest(
                    Guid.NewGuid(),
                    previewPath,
                    CachedContentClass.Thumbnail));

                var isFilled = await WaitForCacheAsync(previewPath);
                await service.StopAsync(CancellationToken.None);

                // Assert
                isFilled.Should().BeTrue(
                    "because a preview the database does not hold must still be warmed from the file "
                    + "beside the media");

                _cache.TryGet(previewPath, out var cached).Should().BeTrue(
                    "because the fallback fills the same cache entry the database branch would have");
                cached.Length.Should().Be(onDisc.Length,
                    "because the bytes held are the image that was read from disc");
            }
            finally
            {
                File.Delete(previewPath);
            }
        }

        /// <summary>
        /// The database branch is for previews alone - a film is never stored in SQLite, and consulting it
        /// for one would be a query per media file for an answer that is always null.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ForAMediaFile_ReadsTheDiscAndNeverTheDatabase()
        {
            // Arrange - the repository would answer with these bytes if it were asked, and it must not be.
            var onDisc = new byte[] { 7, 7, 7 };
            var repository = new RecordingMediaFileRepository { ThumbnailContent = [1, 2, 3, 4, 5, 6, 7, 8] };
            var mediaPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mkv");

            await File.WriteAllBytesAsync(mediaPath, onDisc, CancellationToken.None);

            try
            {
                using var service = CreateService(repository);
                await service.StartAsync(CancellationToken.None);

                // Act
                _ = _backlog.TryEnqueue(new MediaCacheRequest(Guid.NewGuid(), mediaPath));

                var isFilled = await WaitForCacheAsync(mediaPath);
                await service.StopAsync(CancellationToken.None);

                // Assert
                isFilled.Should().BeTrue("because a media file is warmed from the filesystem as it always was");

                _cache.TryGet(mediaPath, out var cached).Should().BeTrue(
                    "because the media branch fills the cache under the file's own path");
                cached.Length.Should().Be(onDisc.Length,
                    "because the bytes held are the file's, which proves the stored-preview branch was not "
                    + "taken for a media file");
            }
            finally
            {
                File.Delete(mediaPath);
            }
        }

        private MediaCacheFillHostedService CreateService(IMediaFileRepository repository)
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton(repository);

            var provider = services.BuildServiceProvider();

            // Already ready: this fixture supplies the repository directly rather than a database, so
            // there is no schema for the drain to wait on.
            var readySignal = new DatabaseReadySignal();
            readySignal.MarkReady();

            return new MediaCacheFillHostedService(
                _backlog,
                _cache,
                provider.GetRequiredService<IServiceScopeFactory>(),
                readySignal,
                NullLogger<MediaCacheFillHostedService>.Instance);
        }

        /// <summary>
        /// Polls rather than signals: the drain reports nothing when it finishes an item, and the cache
        /// entry appearing is the observable effect under test.
        /// </summary>
        private async Task<bool> WaitForCacheAsync(string filePath)
        {
            using var timeout = new CancellationTokenSource(_fillTimeout);

            while (!timeout.IsCancellationRequested)
            {
                if (_cache.TryGet(filePath, out _))
                {
                    return true;
                }

                try
                {
                    await Task.Delay(_pollInterval, timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            return false;
        }
    }
}
