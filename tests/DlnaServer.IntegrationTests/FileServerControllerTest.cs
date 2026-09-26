using System.Net;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Delivery;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Subtitles;
using DlnaServer.Host.Controllers;
using DlnaServer.Host.Delivery;
using DlnaServer.Host.Diagnostics;
using DlnaServer.Upnp.Ssdp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the renderer-facing delivery surface, which had no tests at all.
    /// </summary>
    /// <remarks>
    /// Its collaborators are well covered in isolation - <c>MediaContentResolverTest</c> for the
    /// cache-or-disc decision, <c>ServedFileCacheTest</c> for the cache - but the controller's own
    /// branching was not extracted anywhere those reach, and it is the one surface a television actually
    /// uses. The ordering test below pins a defect that has already shipped once.
    /// </remarks>
    [TestFixture]
    internal sealed class FileServerControllerTest
    {
        private static readonly Guid _fileId = new("11111111-1111-1111-1111-111111111111");
        private static readonly Guid _thumbnailId = new("22222222-2222-2222-2222-222222222222");
        private static readonly IPAddress _advertisedAddress = IPAddress.Parse("192.168.1.10");

        private string _root = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Directory.CreateTempSubdirectory("dlna-delivery-").FullName;
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, recursive: true);
        }

        /// <summary>
        /// A file still held in memory is served even though it has gone from disc.
        /// </summary>
        /// <remarks>
        /// <b>This has shipped as a defect once.</b> The existence check ran before the cache was
        /// consulted, which turned a still-cached, still-streamable film into a 404 on the one port a
        /// television uses - and <c>IServedFileCache.Evict</c>'s contract says deletion is deliberately
        /// not a reason to evict, precisely so a renderer mid-stream is not cut off. Nothing pinned the
        /// ordering, so a future edit could reintroduce it silently.
        /// </remarks>
        [Test]
        public async Task GetFile_WhenTheFileIsGoneFromDiscButStillCached_ServesItFromMemory()
        {
            // Arrange - indexed, cached, and deleted from disc.
            var missingPath = Path.Combine(_root, "deleted.mkv");
            var payload = new byte[] { 1, 2, 3, 4 };

            var controller = CreateController(
                new RecordingMediaFileRepository { File = CreateFile(missingPath) },
                new FakeContentResolver { Source = MediaContentSource.FromCache(payload) },
                new FakeCache());

            // Act
            var result = await controller.GetFile(_fileId, cancellationToken: CancellationToken.None);

            // Assert
            _ = result.Should().BeOfType<FileStreamResult>(
                "because the bytes are in memory, so the file being gone from disc is not a reason to "
                + "stop a playback that is already under way");
        }

        [Test]
        public async Task GetFile_WhenTheFileIsNotIndexed_ReturnsNotFound()
        {
            // Arrange
            var controller = CreateController(
                new RecordingMediaFileRepository(),
                new FakeContentResolver(),
                new FakeCache());

            // Act
            var result = await controller.GetFile(_fileId, cancellationToken: CancellationToken.None);

            // Assert
            _ = result.Should().BeOfType<NotFoundResult>(
                "because an identifier the library does not know cannot be served from anywhere");
        }

        [Test]
        public async Task GetFile_WhenNotCachedAndGoneFromDisc_ReturnsNotFound()
        {
            // Arrange
            var controller = CreateController(
                new RecordingMediaFileRepository { File = CreateFile(Path.Combine(_root, "deleted.mkv")) },
                new FakeContentResolver { Source = MediaContentSource.FromDisc() },
                new FakeCache());

            // Act
            var result = await controller.GetFile(_fileId, cancellationToken: CancellationToken.None);

            // Assert
            _ = result.Should().BeOfType<NotFoundResult>(
                "because with nothing in memory and nothing on disc there is genuinely nothing to send");
        }

        /// <summary>
        /// The database copy is preferred over the file, and filling the cache from it is what makes the
        /// next request free.
        /// </summary>
        [Test]
        public async Task GetThumbnail_WhenStoredInTheDatabase_ServesItAndFillsTheCache()
        {
            // Arrange - a real file on disc as well, so preferring the database is what the test proves.
            var thumbnailPath = Path.Combine(_root, "poster.jpg");
            await File.WriteAllBytesAsync(thumbnailPath, [9, 9, 9]);

            var cache = new FakeCache();
            var controller = CreateController(
                new RecordingMediaFileRepository
                {
                    Thumbnail = CreateThumbnail(thumbnailPath, hasStoredContent: true),
                    ThumbnailContent = [1, 2],
                },
                new FakeContentResolver(),
                cache);

            // Act
            var result = await controller.GetThumbnail(_thumbnailId, cancellationToken: CancellationToken.None);

            // Assert
            _ = result.Should().BeOfType<FileStreamResult>(
                "because the stored copy is reachable through SQLite's own page cache while the file is a "
                + "separate seek, which is why Thumbnails.StoreInDatabase is worth setting");
            cache.Stored.Should().ContainKey(thumbnailPath,
                "because it is keyed by the file's path, so a later request hits memory whichever source "
                + "happened to fill it");
        }

        [Test]
        public async Task GetThumbnail_WhenNothingIsAvailable_ReturnsNotFound()
        {
            // Arrange - indexed, but no stored copy and no file on disc.
            var controller = CreateController(
                new RecordingMediaFileRepository
                {
                    Thumbnail = CreateThumbnail(Path.Combine(_root, "gone.jpg"), hasStoredContent: false),
                },
                new FakeContentResolver(),
                new FakeCache());

            // Act
            var result = await controller.GetThumbnail(_thumbnailId, cancellationToken: CancellationToken.None);

            // Assert
            _ = result.Should().BeOfType<NotFoundResult>(
                "because every source has been tried by this point");
        }

        /// <summary>
        /// A probe answers from the recorded size without reading anything.
        /// </summary>
        /// <remarks>
        /// Most renderers HEAD before they GET, so this answering 404 while <c>GetFile</c> answers 200
        /// from the cache would stop playback at the probe.
        /// </remarks>
        [Test]
        public async Task HeadFile_WhenTheFileIsGoneFromDiscButStillCached_AgreesWithGetFile()
        {
            // Arrange
            var missingPath = Path.Combine(_root, "deleted.mkv");
            var cache = new FakeCache();
            cache.Stored[missingPath] = new byte[] { 1, 2, 3, 4 };

            var controller = CreateController(
                new RecordingMediaFileRepository { File = CreateFile(missingPath) },
                new FakeContentResolver(),
                cache);

            // Act
            var result = await controller.HeadFile(_fileId, cancellationToken: CancellationToken.None);

            // Assert
            _ = result.Should().NotBeOfType<NotFoundResult>(
                "because a probe must not contradict the GET beside it - a renderer that HEADs first "
                + "would never start the stream");
        }

        [Test]
        public async Task GetSubtitle_ForALinkedFile_ServesIt()
        {
            // Arrange
            var film = CreateFile(Path.Combine(_root, "film.mkv"));
            File.WriteAllText(Path.Combine(_root, "film.en.srt"), "1\n00:00:01,000 --> 00:00:02,000\nHello\n");
            var subtitles = new FakeSubtitleRepository();
            var subtitle = CreateSubtitle(film, "film.en.srt");
            subtitles.Links.Add(subtitle);

            var controller = CreateController(
                new RecordingMediaFileRepository { File = film },
                new FakeContentResolver(),
                new FakeCache(),
                subtitles);

            // Act
            var result = await controller.GetSubtitle(subtitle.PublicId, cancellationToken: CancellationToken.None);

            // Assert
            result.Should().BeOfType<PhysicalFileResult>("because a linked subtitle beside the film is served from disc")
                .Which.ContentType.Should().Be("text/srt", "because the MIME comes from the subtitle's own extension");
        }

        [Test]
        public async Task GetSubtitle_ForAStoredPathOutsideTheFilmsFolder_RefusesIt()
        {
            // Arrange
            var film = CreateFile(Path.Combine(_root, "films", "film.mkv"));
            File.WriteAllText(Path.Combine(_root, "secret.srt"), "x");
            var subtitles = new FakeSubtitleRepository();
            var subtitle = CreateSubtitle(film, "../secret.srt");
            subtitles.Links.Add(subtitle);

            var controller = CreateController(
                new RecordingMediaFileRepository { File = film },
                new FakeContentResolver(),
                new FakeCache(),
                subtitles);

            // Act
            var result = await controller.GetSubtitle(subtitle.PublicId, cancellationToken: CancellationToken.None);

            // Assert
            result.Should().BeOfType<NotFoundResult>(
                "because the stored path is checked again before serving, and one that climbs out of the film's folder is refused");
        }

        [Test]
        public async Task GetFile_WhenASamsungAsksForCaptionInfo_AnswersWithTheSubtitleUrl()
        {
            // Arrange
            var path = Path.Combine(_root, "film.mkv");
            File.WriteAllBytes(path, [1, 2, 3]);
            var film = CreateFile(path) with { HasSubtitleFiles = true };
            var subtitles = new FakeSubtitleRepository();
            var subtitle = CreateSubtitle(film, "film.srt");
            subtitles.Links.Add(subtitle);

            var controller = CreateController(
                new RecordingMediaFileRepository { File = film },
                new FakeContentResolver(),
                new FakeCache(),
                subtitles);
            controller.HttpContext.Connection.LocalIpAddress = _advertisedAddress;
            controller.HttpContext.Connection.LocalPort = 26852;
            controller.HttpContext.Request.Host = new HostString("attacker.example:80");
            controller.HttpContext.Request.Headers["getCaptionInfo.sec"] = "1";

            // Act
            _ = await controller.GetFile(_fileId, cancellationToken: CancellationToken.None);

            // Assert
            controller.HttpContext.Response.Headers["CaptionInfo.sec"].ToString().Should().Be(
                $"http://192.168.1.10:26852/fileserver/subtitle/{subtitle.PublicId}.srt",
                "because a Samsung television reads the subtitle's URL from this header, built from the "
                + "advertised address the request arrived on and never from the Host header it sent");
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

        private static FileServerController CreateController(
            RecordingMediaFileRepository repository,
            FakeContentResolver content,
            FakeCache cache,
            FakeSubtitleRepository? subtitles = null)
        {
            var options = new StaticOptionsMonitor<DlnaOptions>(new DlnaOptions());

            return new FileServerController(
                repository,
                subtitles ?? new FakeSubtitleRepository(),
                new TemporaryFolderVisibility(options, TimeProvider.System),
                new UpnpDeviceRegistry(new StubAddressProvider(), "test", 26852),
                cache,
                content,
                options,
                NullLogger<FileServerController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
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

        private static ThumbnailDto CreateThumbnail(string filePath, bool hasStoredContent)
        {
            return new ThumbnailDto
            {
                PublicId = _thumbnailId,
                MediaFilePublicId = _fileId,
                FilePath = filePath,
                Mime = DlnaMime.ImageJpeg,
                Width = 160,
                Height = 90,
                SizeInBytes = 3,
                HasStoredContent = hasStoredContent,
            };
        }

        private sealed class FakeContentResolver : IMediaContentResolver
        {
            public MediaContentSource Source { get; init; } = MediaContentSource.FromDisc();

            public MediaContentSource Resolve(MediaFileDto file)
            {
                return Source;
            }
        }

        private sealed class FakeCache : IServedFileCache
        {
            public Dictionary<string, byte[]> Stored { get; } = new(StringComparer.Ordinal);

            public bool IsEnabled => true;

            public long BudgetInBytes => 1024 * 1024;

            public long MaxFileSizeInBytes => 1024 * 1024;

            public bool TryGet(string filePath, out ReadOnlyMemory<byte> content)
            {
                if (Stored.TryGetValue(filePath, out var found))
                {
                    content = found;
                    return true;
                }

                content = ReadOnlyMemory<byte>.Empty;
                return false;
            }

            public Task<ReadOnlyMemory<byte>> LoadAsync(
                string filePath,
                CachedContentClass contentClass,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(TryGet(filePath, out var content)
                    ? content
                    : ReadOnlyMemory<byte>.Empty);
            }

            public void Store(string filePath, CachedContentClass contentClass, ReadOnlyMemory<byte> content)
            {
                Stored[filePath] = content.ToArray();
            }

            public ServedFileCacheReport Describe()
            {
                throw new NotSupportedException("Not part of the delivery path under test.");
            }

            public int Clear()
            {
                throw new NotSupportedException("Not part of the delivery path under test.");
            }

            public bool Evict(string filePath)
            {
                return Stored.Remove(filePath);
            }
        }

        private sealed class StubAddressProvider : ILocalAddressProvider
        {
            public IReadOnlyList<IPAddress> GetBroadcastableAddresses()
            {
                return [_advertisedAddress];
            }
        }
    }
}
