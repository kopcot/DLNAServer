using System.IO.Compression;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Delivery;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Subtitles;
using DlnaServer.Host.Configuration;
using DlnaServer.Host.Controllers;
using DlnaServer.Host.Delivery;
using DlnaServer.Host.Delivery.Caching;
using DlnaServer.Host.Delivery.Prefetch;
using DlnaServer.Host.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the admin port's door onto the same content <see cref="FileServerController"/> serves, which
    /// is meant to behave identically and had drifted from it twice, and the subtitle download beside it.
    /// </summary>
    [TestFixture]
    internal sealed class AdminMediaControllerTest
    {
        private static readonly Guid _fileId = new("33333333-3333-3333-3333-333333333333");
        private static readonly Guid _thumbnailId = new("44444444-4444-4444-4444-444444444444");

        private string _root = null!;
        private ServedFileCache _cache = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Directory.CreateTempSubdirectory("dlna-admin-media-").FullName;
            _cache = new ServedFileCache(
                new StaticOptionsMonitor<DlnaOptions>(new DlnaOptions()),
                TimeProvider.System,
                NullLogger<ServedFileCache>.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            _cache.Dispose();
            Directory.Delete(_root, recursive: true);
        }

        /// <summary>
        /// GetFile serves a file deleted from disc while its bytes are still held, and the probe beside it
        /// answered 404 - the media port had already been fixed for exactly this.
        /// </summary>
        [Test]
        public async Task HeadFile_WhenTheFileIsGoneFromDiscButStillCached_AgreesWithGetFile()
        {
            // Arrange
            var missingPath = Path.Combine(_root, "deleted.mkv");
            _cache.Store(missingPath, CachedContentClass.Media, new byte[] { 1, 2, 3, 4 });
            var controller = CreateController(new RecordingMediaFileRepository { File = CreateFile(missingPath) });

            // Act
            var result = await controller.HeadFile(_fileId, cancellationToken: CancellationToken.None);

            // Assert
            result.Should().BeOfType<FileStreamResult>(
                "because a probe must not contradict the GET that would serve the same file from memory")
                .Which.FileStream.Length.Should().Be(4, "because the cached payload answers for its own length");
        }

        [Test]
        public async Task HeadFile_WhenNeitherOnDiscNorCached_ReturnsNotFound()
        {
            // Arrange
            var controller = CreateController(
                new RecordingMediaFileRepository { File = CreateFile(Path.Combine(_root, "gone.mkv")) });

            // Act
            var result = await controller.HeadFile(_fileId, cancellationToken: CancellationToken.None);

            // Assert
            result.Should().BeOfType<NotFoundResult>("because there is nothing anywhere to describe");
        }

        [Test]
        public async Task GetFile_ForAnSvg_SandboxesTheResponse()
        {
            // Arrange
            var path = Path.Combine(_root, "drawing.svg");
            await File.WriteAllTextAsync(path, "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
            var controller = CreateController(
                new RecordingMediaFileRepository { File = CreateFile(path) with { Mime = DlnaMime.ImageSvgXml } });

            // Act
            _ = await controller.GetFile(_fileId, cancellationToken: CancellationToken.None);

            // Assert
            controller.HttpContext.Response.Headers.ContentSecurityPolicy.ToString().Should().Be("sandbox",
                "because an SVG opened on the admin origin must not be able to run script there");
        }

        [Test]
        public async Task GetThumbnail_WhenStoredInTheDatabase_ServesItAndFillsTheCache()
        {
            // Arrange
            var thumbnailPath = Path.Combine(_root, "poster.jpg");
            var controller = CreateController(new RecordingMediaFileRepository
            {
                Thumbnail = CreateThumbnail(thumbnailPath),
                ThumbnailContent = [1, 2],
            });

            // Act
            var result = await controller.GetThumbnail(_thumbnailId, cancellationToken: CancellationToken.None);

            // Assert
            result.Should().BeOfType<FileStreamResult>("because the stored copy is served when it exists");
            _cache.TryGet(thumbnailPath, out _).Should().BeTrue(
                "because the admin port fills the cache exactly as the media port does, under the file's path");
        }

        [Test]
        public async Task GetSubtitles_WithOneLinkedFile_DownloadsThatFile()
        {
            // Arrange
            var film = CreateFile(Path.Combine(_root, "film.mkv"));
            await File.WriteAllTextAsync(Path.Combine(_root, "film.en.srt"), "1\n00:00:01,000 --> 00:00:02,000\nHello\n");
            var subtitles = new FakeSubtitleRepository();
            subtitles.Links.Add(CreateSubtitle(film, "film.en.srt"));

            var controller = CreateController(new RecordingMediaFileRepository { File = film }, subtitles);

            // Act
            var result = await controller.GetSubtitles(_fileId, cancellationToken: CancellationToken.None);

            // Assert
            result.Should().BeOfType<PhysicalFileResult>(
                    "because a single linked subtitle is downloaded as itself, not wrapped in a zip")
                .Which.FileDownloadName.Should().Be("film.en.srt",
                    "because the browser saves it under the subtitle's own name");
        }

        [Test]
        public async Task GetSubtitles_WithSeveralLinkedFiles_DownloadsOneZipHoldingThemAll()
        {
            // Arrange
            var film = CreateFile(Path.Combine(_root, "film.mkv"));
            Directory.CreateDirectory(Path.Combine(_root, "Subs"));
            await File.WriteAllTextAsync(Path.Combine(_root, "film.en.srt"), "en");
            await File.WriteAllTextAsync(Path.Combine(_root, "Subs", "film.en.srt"), "en, second copy");
            var subtitles = new FakeSubtitleRepository();
            subtitles.Links.Add(CreateSubtitle(film, "film.en.srt"));
            subtitles.Links.Add(CreateSubtitle(film, "Subs/film.en.srt"));

            var controller = CreateController(new RecordingMediaFileRepository { File = film }, subtitles);

            // Act
            var result = await controller.GetSubtitles(_fileId, cancellationToken: CancellationToken.None);

            // Assert
            var zipped = result.Should().BeOfType<FileContentResult>(
                    "because several linked subtitles arrive as one download")
                .Which;
            zipped.FileDownloadName.Should().Be("film.subtitles.zip",
                "because the zip is named after the media file it belongs to");

            using var archive = new ZipArchive(new MemoryStream(zipped.FileContents), ZipArchiveMode.Read);
            archive.Entries.Select(static e => e.FullName).Should().BeEquivalentTo(["film.en.srt", "Subs/film.en.srt"],
                "because each entry keeps its path from the film's folder, so two files of one name stay apart");
        }

        [Test]
        public async Task GetSubtitles_WhenTheOnlyStoredPathClimbsOutOfTheFolder_ReturnsNotFound()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(_root, "films"));
            var film = CreateFile(Path.Combine(_root, "films", "film.mkv"));
            await File.WriteAllTextAsync(Path.Combine(_root, "secret.srt"), "x");
            var subtitles = new FakeSubtitleRepository();
            subtitles.Links.Add(CreateSubtitle(film, "../secret.srt"));

            var controller = CreateController(new RecordingMediaFileRepository { File = film }, subtitles);

            // Act
            var result = await controller.GetSubtitles(_fileId, cancellationToken: CancellationToken.None);

            // Assert
            result.Should().BeOfType<NotFoundResult>(
                "because a stored path the subtitle rule refuses is never served, so nothing is left to download");
        }

        private AdminMediaController CreateController(
            RecordingMediaFileRepository repository,
            FakeSubtitleRepository? subtitles = null)
        {
            var options = new StaticOptionsMonitor<DlnaOptions>(
                new DlnaOptions { Library = { SubtitleFileExtensions = SubtitleFileExtensionDefaults.Create() } });

            return new AdminMediaController(
                repository,
                subtitles ?? new FakeSubtitleRepository(),
                new SubtitleFileChecker(new TemporaryFolderVisibility(options, TimeProvider.System), options),
                _cache,
                new MediaContentResolver(_cache, new MediaCacheBacklog()),
                NullLogger<AdminMediaController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };
        }

        private static SubtitleFileDto CreateSubtitle(MediaFileDto film, string relativePath)
        {
            return new SubtitleFileDto
            {
                PublicId = Guid.NewGuid(),
                MediaFilePublicId = film.PublicId,
                MediaFileFullPath = film.FullPath,
                RelativePath = relativePath,
                Source = SubtitleSource.Automatic,
            };
        }

        private static MediaFileDto CreateFile(string fullPath)
        {
            return new MediaFileDto
            {
                PublicId = _fileId,
                FullPath = fullPath,
                FileName = Path.GetFileName(fullPath),
                Title = Path.GetFileNameWithoutExtension(fullPath),
                Extension = Path.GetExtension(fullPath),
                Mime = DlnaMime.VideoXMatroska,
                UpnpClass = DlnaItemClass.VideoItem,
                SizeInBytes = 4,
                FileCreatedUtc = DateTime.UtcNow,
                FileModifiedUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                IsExcludedFromCache = false,
                HasSubtitleTracks = false,
                HasSubtitleFiles = false,
                ContentStamp = "4:1",
            };
        }

        private static ThumbnailDto CreateThumbnail(string filePath)
        {
            return new ThumbnailDto
            {
                PublicId = _thumbnailId,
                MediaFilePublicId = _fileId,
                FilePath = filePath,
                Mime = DlnaMime.ImageJpeg,
                Width = 160,
                Height = 90,
                SizeInBytes = 2,
                HasStoredContent = true,
            };
        }
    }
}
