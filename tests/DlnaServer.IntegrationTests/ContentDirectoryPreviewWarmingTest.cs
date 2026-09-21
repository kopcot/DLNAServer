using DlnaServer.Core.Configuration;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Delivery;
using DlnaServer.Core.Dlna;
using DlnaServer.Host.Delivery.Prefetch;
using DlnaServer.Host.Upnp.Control;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers what Browse queues for a folder's previews, which is the only moment the server knows which
    /// thirty images a television is about to ask for.
    /// </summary>
    /// <remarks>
    /// The identifier on the queued request is load-bearing and fails silently when it is wrong: the drain
    /// looks the stored image up by it, and the media file's identifier matches no thumbnail row, so the
    /// lookup simply returns nothing and the warm falls back to disc - the exact behaviour this pass
    /// exists to stop, with no error anywhere to say so.
    /// </remarks>
    [TestFixture]
    internal sealed class ContentDirectoryPreviewWarmingTest
    {
        private static readonly string _mediaFolder = Path.Combine(Path.GetTempPath(), "Films");

        [Test]
        public void WarmPreviews_QueuesThePreviewUnderItsOwnIdentifierAndNotTheMediaFile()
        {
            // Arrange
            var thumbnailPublicId = Guid.NewGuid();
            var file = CreateFile("Film.mkv", thumbnailPublicId);
            var backlog = new MediaCacheBacklog();

            // Act
            ContentDirectoryService.WarmPreviews([file], new DlnaOptions(), backlog);

            // Assert
            backlog.Reader.TryRead(out var queued).Should().BeTrue(
                "because a file that has a preview is queued so the image is in memory before the "
                + "television asks for it");

            queued.PublicId.Should().Be(thumbnailPublicId,
                "because the drain reads the stored image by the thumbnail's identifier - the media "
                + "file's would match no row and silently send the warm to the platter");
            queued.PublicId.Should().NotBe(file.PublicId,
                "because the two identifiers are different rows, and passing the media file's is the "
                + "regression this test exists for");
            queued.ContentClass.Should().Be(CachedContentClass.Thumbnail,
                "because the retention and the database branch both key off the content class");
            queued.FilePath.Should().Be(Path.Combine(_mediaFolder, ".@__thumb", "Film.mkv.jpg"),
                "because the path is derived rather than read, which is what keeps Browse free of "
                + "database work");
        }

        [Test]
        public void WarmPreviews_ForAFileWithNoPreview_QueuesNothing()
        {
            // Arrange
            var file = CreateFile("Unprocessed.mkv", thumbnailPublicId: null);
            var backlog = new MediaCacheBacklog();

            // Act
            ContentDirectoryService.WarmPreviews([file], new DlnaOptions(), backlog);

            // Assert
            backlog.Reader.TryRead(out _).Should().BeFalse(
                "because there is no image to warm until the processing pass has generated one");
        }

        [Test]
        public void WarmPreviews_WhenTheSwitchIsOff_QueuesNothing()
        {
            // Arrange
            var file = CreateFile("Film.mkv", Guid.NewGuid());
            var backlog = new MediaCacheBacklog();
            var options = new DlnaOptions();
            options.FileCache.WarmPreviewsOnBrowse = false;

            // Act
            ContentDirectoryService.WarmPreviews([file], options, backlog);

            // Assert
            backlog.Reader.TryRead(out _).Should().BeFalse(
                "because the switch exists so a deployment can stop Browse spending disc reads on "
                + "previews before a renderer has asked for them");
        }

        private static MediaFileDto CreateFile(string fileName, Guid? thumbnailPublicId)
        {
            return new MediaFileDto
            {
                PublicId = Guid.NewGuid(),
                FullPath = Path.Combine(_mediaFolder, fileName),
                FileName = fileName,
                Title = Path.GetFileNameWithoutExtension(fileName),
                Extension = Path.GetExtension(fileName),
                DirectoryPublicId = Guid.NewGuid(),
                Mime = DlnaMime.VideoXMatroska,
                UpnpClass = DlnaItemClass.VideoItem,
                SizeInBytes = 1024,
                FileCreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FileModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsExcludedFromCache = false,
                ContentStamp = "1024:638000000000000000",
                ThumbnailPublicId = thumbnailPublicId,
            };
        }
    }
}
