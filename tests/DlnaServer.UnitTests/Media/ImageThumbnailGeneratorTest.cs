using DlnaServer.Core.Dlna;
using DlnaServer.Media.Processing;
using DlnaServer.Media.Processing.Provisioning;
using DlnaServer.Media.Processing.Thumbnails;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace DlnaServer.UnitTests.Media
{
    /// <summary>
    /// Covers what thumbnailing does when a link stands where a thumbnail or its folder belongs.
    /// </summary>
    /// <remarks>
    /// The thumbnail folder lives inside a source folder, so anyone able to write to the share can plant a
    /// link there. Real files and real links, because the guards are about what the filesystem does.
    /// </remarks>
    [TestFixture]
    internal sealed class ImageThumbnailGeneratorTest
    {
        private static readonly MediaProcessingSettings _settings = new(
            MaxWidth: 160,
            MaxHeight: 160,
            Quality: 80,
            ThumbnailMime: DlnaMime.ImageJpeg,
            StoreThumbnailContent: true,
            AllowFFmpegDownload: false,
            ReadContainerTags: false);

        private string _root = null!;
        private string _outside = null!;
        private string _picture = null!;

        [SetUp]
        public void SetUp()
        {
            var temporaryFolder = Path.GetFullPath(Path.GetTempPath());

            _root = Path.Combine(temporaryFolder, $"dlna-thumb-root-{Guid.NewGuid():N}");
            _outside = Path.Combine(temporaryFolder, $"dlna-thumb-outside-{Guid.NewGuid():N}");
            _picture = Path.Combine(_root, "photo.png");

            _ = Directory.CreateDirectory(_root);
            _ = Directory.CreateDirectory(_outside);
            WritePng(_picture);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, recursive: true);
            Directory.Delete(_outside, recursive: true);
        }

        [Test]
        public void Generate_WhenALinkStandsAtTheTarget_ReplacesTheLinkAndLeavesWhatItPointsAtAlone()
        {
            // Arrange
            var victim = Path.Combine(_outside, "precious.txt");
            var target = Path.Combine(_root, "photo.png.jpg");

            File.WriteAllText(victim, "keep me");
            CreateLinkOrIgnore(() => File.CreateSymbolicLink(target, victim));

            // Act
            var thumbnail = Generator().Generate(_picture, target, Request());

            // Assert
            thumbnail.Should().NotBeNull("because the source is an ordinary picture");
            File.ReadAllText(victim).Should().Be("keep me",
                "because writing through the link would overwrite a file outside the library");
            new FileInfo(target).LinkTarget.Should().BeNull(
                "because the thumbnail replaces the link rather than writing to what it points at");
        }

        [Test]
        public void Generate_ForAnOrdinaryTarget_LeavesOnlyTheThumbnailBehind()
        {
            // Arrange
            var folder = Path.Combine(_root, ".@__thumb");
            var target = Path.Combine(folder, "photo.png.jpg");

            // Act
            var thumbnail = Generator().Generate(_picture, target, Request());

            // Assert
            thumbnail.Should().NotBeNull("because the source is an ordinary picture");
            Directory.GetFiles(folder).Should().Equal([target],
                "because the temporary file the thumbnail is written to is renamed over the target");
        }

        [Test]
        public async Task GenerateThumbnailAsync_WhenTheThumbnailFolderIsALink_WritesNothingThroughIt()
        {
            // Arrange
            var folder = Path.Combine(_root, ".@__thumb");

            CreateLinkOrIgnore(() => Directory.CreateSymbolicLink(folder, _outside));

            // Act
            var thumbnail = await Processor().GenerateThumbnailAsync(
                filePath: _picture,
                mime: DlnaMime.ImagePng,
                targetPath: Path.Combine(folder, "photo.png.jpg"),
                settings: _settings,
                allowAdoption: true,
                notOlderThanUtc: null,
                knownDuration: null,
                cancellationToken: CancellationToken.None);

            // Assert
            thumbnail.Should().BeNull("because a linked thumbnail folder is refused");
            Directory.GetFiles(_outside).Should().BeEmpty(
                "because nothing may be written into the folder the link leads to");
        }

        [Test]
        public async Task GenerateThumbnailAsync_WhenALinkStandsAtTheThumbnail_DoesNotAdoptWhatItPointsAt()
        {
            // Arrange
            var folder = Path.Combine(_root, ".@__thumb");
            var secret = Path.Combine(_outside, "secret.png");
            var target = Path.Combine(folder, "photo.png.jpg");

            _ = Directory.CreateDirectory(folder);
            WritePng(secret);
            CreateLinkOrIgnore(() => File.CreateSymbolicLink(target, secret));

            // Act
            var thumbnail = await Processor().GenerateThumbnailAsync(
                filePath: _picture,
                mime: DlnaMime.ImagePng,
                targetPath: target,
                settings: _settings,
                allowAdoption: true,
                notOlderThanUtc: null,
                knownDuration: null,
                cancellationToken: CancellationToken.None);

            // Assert
            thumbnail.Should().BeNull(
                "because adopting the link would read a file outside the library and serve it as a preview");
        }

        private static ImageThumbnailGenerator Generator()
        {
            return new ImageThumbnailGenerator(NullLogger<ImageThumbnailGenerator>.Instance);
        }

        private static MediaProcessor Processor()
        {
            return new MediaProcessor(
                new AvailableFFmpeg(),
                Generator(),
                NullLogger<MediaProcessor>.Instance,
                getMediaInfo: static (_, _) => throw new InvalidOperationException("No probe is expected"));
        }

        private static ThumbnailRequest Request()
        {
            return new ThumbnailRequest(
                MaxWidth: 160,
                MaxHeight: 160,
                Quality: 80,
                Mime: DlnaMime.ImageJpeg,
                StoreContent: false);
        }

        private static void WritePng(string path)
        {
            using var bitmap = new SKBitmap(8, 8);
            bitmap.Erase(SKColors.CornflowerBlue);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

            File.WriteAllBytes(path, encoded.ToArray());
        }

        private static void CreateLinkOrIgnore(Func<FileSystemInfo> createLink)
        {
            try
            {
                _ = createLink();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Windows refuses a symlink without developer mode or elevation. Linux is what the server
                // is deployed on, and CI's ubuntu leg runs these.
                Assert.Ignore($"This machine does not allow creating a symbolic link: {exception.Message}");
            }
        }

        private sealed class AvailableFFmpeg : IFFmpegProvisioner
        {
            public Task<bool> EnsureAvailableAsync(bool allowDownload, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(true);
            }
        }
    }
}
