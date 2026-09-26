using System.Text.Json;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Delivery;
using DlnaServer.Core.Diagnostics;
using DlnaServer.Core.Hosting;
using DlnaServer.Host.Configuration;
using DlnaServer.Host.Controllers;
using DlnaServer.Host.Diagnostics;
using DlnaServer.Host.Gena;
using DlnaServer.Host.Indexing;
using DlnaServer.Media;
using DlnaServer.Persistence;
using DlnaServer.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers <c>/manage/directory/{id}</c> against a real index, because the paging it gained is only
    /// meaningful over real rows.
    /// </summary>
    [TestFixture]
    internal sealed class ManageControllerDirectoryTest
    {
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        private string _mediaRoot = null!;
        private string _databasePath = null!;
        private ServiceProvider _provider = null!;

        [SetUp]
        public async Task SetUp()
        {
            _mediaRoot = Directory.CreateTempSubdirectory("dlna-manage-dir-").FullName;
            _databasePath = Path.Combine(Path.GetTempPath(), $"dlna-manage-dir-{Guid.NewGuid():N}.sqlite");

            var options = new DlnaOptions();
            options.Library.SourceFolders = [_mediaRoot];
            options.Library.ExcludeFolders = [options.Thumbnails.SubFolderName];
            options.Library.MediaFileExtensions[".mkv"] = new MediaExtensionOptions { Mime = "VideoXMatroska" };

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
            _ = services.AddSingleton<IOptionsMonitor<DlnaOptions>>(new StaticOptionsMonitor<DlnaOptions>(options));

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
        /// A folder of thousands of files used to come back in one unbounded response.
        /// </summary>
        [Test]
        public async Task GetDirectoryAsync_WithTakeAndSkip_ReturnsOnePageAndTheTotals()
        {
            // Arrange
            CreateFile("a.mkv");
            CreateFile("b.mkv");
            CreateFile("c.mkv");
            CreateFile(Path.Combine("one", "x.mkv"));
            CreateFile(Path.Combine("two", "y.mkv"));
            CreateFile(Path.Combine("three", "z.mkv"));

            using var scope = _provider.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>().IndexAsync(CancellationToken.None);

            var directories = scope.ServiceProvider.GetRequiredService<IMediaDirectoryRepository>();
            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();
            var root = (await directories.GetSourceRootsAsync(CancellationToken.None)).Single();
            var controller = CreateController();

            // Act
            var firstPage = Read(await controller.GetDirectoryAsync(
                id: root.PublicId,
                directories: directories,
                files: files,
                cancellationToken: CancellationToken.None,
                skip: 0,
                take: 2));
            var secondPage = Read(await controller.GetDirectoryAsync(
                id: root.PublicId,
                directories: directories,
                files: files,
                cancellationToken: CancellationToken.None,
                skip: 2,
                take: 2));

            // Assert
            firstPage.GetProperty("files").GetArrayLength().Should().Be(2, "because take bounds the page");
            firstPage.GetProperty("children").GetArrayLength().Should().Be(2, "because take bounds both listings");
            firstPage.GetProperty("fileCount").GetInt32().Should().Be(3,
                "because the total is what tells a caller there is more to fetch");
            firstPage.GetProperty("childCount").GetInt32().Should().Be(3,
                "because the total is what tells a caller there is more to fetch");
            secondPage.GetProperty("files").GetArrayLength().Should().Be(1, "because skip moves past the first page");
        }

        [Test]
        public async Task GetDirectoryAsync_WithAnOversizedTake_IsCapped()
        {
            // Arrange
            CreateFile("a.mkv");

            using var scope = _provider.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>().IndexAsync(CancellationToken.None);

            var directories = scope.ServiceProvider.GetRequiredService<IMediaDirectoryRepository>();
            var root = (await directories.GetSourceRootsAsync(CancellationToken.None)).Single();

            // Act
            var page = Read(await CreateController().GetDirectoryAsync(
                id: root.PublicId,
                directories: directories,
                files: scope.ServiceProvider.GetRequiredService<IMediaFileRepository>(),
                cancellationToken: CancellationToken.None,
                skip: -5,
                take: int.MaxValue));

            // Assert
            page.GetProperty("files").GetArrayLength().Should().Be(1,
                "because a negative skip is read as the start and an oversized take is clamped, not refused");
        }

        private static ManageController CreateController()
        {
            var options = new StaticOptionsMonitor<DlnaOptions>(new DlnaOptions());

            return new ManageController(
                new RecordingApplicationLifetime(),
                new RestartSignal(),
                new DatabaseResetSignal(),
                new NoOpServedFileCache(),
                new ApiBlocker(TimeProvider.System),
                new SubscriptionStore(TimeProvider.System),
                options,
                NullLogger<ManageController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };
        }

        private static JsonElement Read(IActionResult result)
        {
            var value = result.Should().BeOfType<OkObjectResult>("because the directory exists").Which.Value;

            return JsonSerializer.SerializeToElement(value, _json);
        }

        private void CreateFile(string relativePath)
        {
            var fullPath = Path.Combine(_mediaRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, new byte[16]);
        }
    }
}
