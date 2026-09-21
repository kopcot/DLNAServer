using DlnaServer.Media.Processing.Metadata;
using SkiaSharp;

namespace DlnaServer.UnitTests.Media
{
    /// <summary>
    /// Pictures are read by MetadataExtractor rather than ffprobe, which reports a JPEG as a one-frame
    /// video and never looks at its EXIF block.
    /// </summary>
    [TestFixture]
    internal sealed class ImageTagReaderTest
    {
        private string _directory = null!;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"dlna-imagetags-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp folder is not worth failing a test over.
            }
        }

        [Test]
        public void Read_ForAJpeg_ReturnsWhatThePictureSaysAboutItself()
        {
            // Arrange
            var path = CreateJpeg(width: 64, height: 48);

            // Act
            var tags = ImageTagReader.Read(path);

            // Assert
            tags.Should().NotBeEmpty("because even a plain JPEG describes its own format and dimensions");
            tags.Should().OnlyContain(t => t.StreamIndex == null,
                "because a picture has no tracks to attribute a tag to");
            tags.Should().Contain(t => t.Name.Contains("Image Height", StringComparison.Ordinal),
                "because the reader must surface the picture's own dimensions");
            tags.Should().Contain(t => t.Value.Contains("48", StringComparison.Ordinal),
                "because the height written into the file is what should come back");
        }

        /// <remarks>
        /// A name alone repeats across directories - Compression exists in several - so the directory is
        /// qualified into it, which is also what groups EXIF, GPS and IPTC readably in the panel.
        /// </remarks>
        [Test]
        public void Read_QualifiesEveryNameWithItsDirectory()
        {
            // Arrange
            var path = CreateJpeg(width: 32, height: 32);

            // Act
            var tags = ImageTagReader.Read(path);

            // Assert
            tags.Should().OnlyContain(t => t.Name.Contains(" - ", StringComparison.Ordinal),
                "because every tag is stored as \"<directory> - <name>\"");
        }

        [Test]
        public void Read_ForAFileThatIsNotAPicture_ReturnsNothingRatherThanThrowing()
        {
            // Arrange
            var path = Path.Combine(_directory, "NotAPicture.jpg");
            File.WriteAllText(path, "this is not an image");

            // Act
            var tags = ImageTagReader.Read(path);

            // Assert
            tags.Should().BeEmpty(
                "because an unreadable picture must cost the file its tags and nothing else - it is still "
                + "indexed, and its other metadata still stored");
        }

        [Test]
        public void Read_ForAMissingFile_ReturnsNothingRatherThanThrowing()
        {
            // Arrange
            var path = Path.Combine(_directory, "Gone.jpg");

            // Act
            var tags = ImageTagReader.Read(path);

            // Assert
            tags.Should().BeEmpty("because a file deleted mid-pass is an expected input, not a defect");
        }

        private string CreateJpeg(int width, int height)
        {
            var path = Path.Combine(_directory, $"Picture-{width}x{height}.jpg");

            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.CornflowerBlue);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
            using var stream = File.Create(path);
            data.SaveTo(stream);

            return path;
        }
    }
}
