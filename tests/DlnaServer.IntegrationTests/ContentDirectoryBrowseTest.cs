using System.Net;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Delivery;
using DlnaServer.Core.Diagnostics;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Hosting;
using DlnaServer.Host.Configuration;
using DlnaServer.Host.Delivery.Prefetch;
using DlnaServer.Host.Diagnostics;
using DlnaServer.Host.Indexing;
using DlnaServer.Host.Upnp.Control;
using DlnaServer.Media;
using DlnaServer.Persistence;
using DlnaServer.Persistence.Repositories;
using DlnaServer.Upnp.Ssdp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers a whole Browse over a real index, for what only shows across a page of items.
    /// </summary>
    [TestFixture]
    internal sealed class ContentDirectoryBrowseTest
    {
        private string _mediaRoot = null!;
        private string _databasePath = null!;
        private DlnaOptions _options = null!;
        private ServiceProvider _provider = null!;

        [SetUp]
        public async Task SetUp()
        {
            _mediaRoot = Directory.CreateTempSubdirectory("dlna-browse-").FullName;
            _databasePath = Path.Combine(Path.GetTempPath(), $"dlna-browse-{Guid.NewGuid():N}.sqlite");

            _options = new DlnaOptions();
            _options.Library.SourceFolders = [_mediaRoot];
            _options.Library.ExcludeFolders = [_options.Thumbnails.SubFolderName];
            _options.Library.MediaFileExtensions[".mkv"] = new MediaExtensionOptions { Mime = "VideoXMatroska" };
            _options.Library.SubtitleFileExtensions[".srt"] = DlnaMedia.Video;
            _options.Compatibility.SendSubtitles = true;

            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddSingleton(TimeProvider.System);
            _ = services.AddSingleton<ITemporaryFolderVisibility, TemporaryFolderVisibility>();
            _ = services.AddDlnaPersistence($"Data Source={_databasePath}");
            _ = services.AddDlnaMedia();
            _ = services.AddScoped<ILibraryIndexer, LibraryIndexer>();
            _ = services.AddSingleton<ILibraryChangeSignal, LibraryChangeSignal>();
            _ = services.AddSingleton<ISourceFolderChecker, SourceFolderChecker>();
            _ = services.AddSingleton<IServedFileCache, NoOpServedFileCache>();
            _ = services.AddSingleton<ILibraryIndexLock, LibraryIndexLock>();
            _ = services.AddSingleton<IOptionsMonitor<DlnaOptions>>(new StaticOptionsMonitor<DlnaOptions>(_options));

            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(CancellationToken.None);
        }

        [TearDown]
        public async Task TearDown()
        {
            await _provider.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            Directory.Delete(_mediaRoot, recursive: true);

            foreach (var file in Directory.EnumerateFiles(
                Path.GetDirectoryName(_databasePath)!,
                Path.GetFileName(_databasePath) + "*"))
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// The subtitle types used to be read from the options once per mapped item.
        /// </summary>
        /// <remarks>
        /// While configuration is invalid every read of the options logs an error, so a page of films with
        /// subtitles logged one error per film.
        /// </remarks>
        [Test]
        public async Task Browse_APageOfFilmsWithSubtitles_ReadsTheOptionsAsOftenAsAPageOfOne()
        {
            // Arrange
            foreach (var name in new[] { "a", "b", "c" })
            {
                CreateFile($"{name}.mkv");
                CreateFile($"{name}.en.srt");
            }

            using var scope = _provider.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>().IndexAsync(CancellationToken.None);

            var directories = scope.ServiceProvider.GetRequiredService<IMediaDirectoryRepository>();
            var root = (await directories.GetSourceRootsAsync(CancellationToken.None)).Single();
            var counting = new CountingOptionsMonitor(_options);
            var service = new ContentDirectoryService(
                directories,
                scope.ServiceProvider.GetRequiredService<IMediaFileRepository>(),
                scope.ServiceProvider.GetRequiredService<ISubtitleRepository>(),
                new HttpContextAccessor(),
                new UpnpDeviceRegistry(new NoAddressProvider(), "test", 26852),
                new MediaCacheBacklog(),
                counting,
                NullLogger<ContentDirectoryService>.Instance);

            // Act
            var single = await service.Browse(
                ObjectID: root.PublicId.ToString(),
                BrowseFlag: "BrowseDirectChildren",
                Filter: "*",
                StartingIndex: 0,
                RequestedCount: 1,
                SortCriteria: string.Empty);
            var readsForOne = counting.Reads;

            counting.Reads = 0;

            var page = await service.Browse(
                ObjectID: root.PublicId.ToString(),
                BrowseFlag: "BrowseDirectChildren",
                Filter: "*",
                StartingIndex: 0,
                RequestedCount: 3,
                SortCriteria: string.Empty);
            var readsForThree = counting.Reads;

            // Assert
            single.Didl.Items.Should().ContainSingle("because the first page holds one film")
                .Which.CaptionInfo.Should().NotBeNull(
                    "because the film's linked subtitle is offered with it, so the per-item path really ran");
            page.Didl.Items.Should().HaveCount(3, "because the second page holds all three films");
            readsForThree.Should().Be(readsForOne,
                "because one page is one decision about which subtitle types are offered, not one read per film");
        }

        private void CreateFile(string relativePath)
        {
            File.WriteAllBytes(Path.Combine(_mediaRoot, relativePath), new byte[16]);
        }

        private sealed class CountingOptionsMonitor : IOptionsMonitor<DlnaOptions>
        {
            private readonly DlnaOptions _value;

            public CountingOptionsMonitor(DlnaOptions value)
            {
                _value = value;
            }

            public int Reads { get; set; }

            public DlnaOptions CurrentValue
            {
                get
                {
                    Reads++;

                    return _value;
                }
            }

            public DlnaOptions Get(string? name)
            {
                return CurrentValue;
            }

            public IDisposable? OnChange(Action<DlnaOptions, string?> listener)
            {
                return null;
            }
        }

        private sealed class NoAddressProvider : ILocalAddressProvider
        {
            public IReadOnlyList<IPAddress> GetBroadcastableAddresses()
            {
                return [];
            }
        }
    }
}
