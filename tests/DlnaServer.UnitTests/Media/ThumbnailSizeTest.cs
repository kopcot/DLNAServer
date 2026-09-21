using DlnaServer.Core.Files;

namespace DlnaServer.UnitTests.Media
{
    [TestFixture]
    internal sealed class ThumbnailSizeTest
    {
        [TestCase(1920, 1080, 480, 360, 480, 270, TestName = "Landscape is bounded by width")]
        [TestCase(1080, 1920, 480, 360, 202, 360, TestName = "Portrait is bounded by height")]
        [TestCase(1000, 1000, 480, 360, 360, 360, TestName = "Square is bounded by the smaller limit")]
        public void Calculate_ScalesDownPreservingAspectRatio(
            int sourceWidth,
            int sourceHeight,
            int maxWidth,
            int maxHeight,
            int expectedWidth,
            int expectedHeight)
        {
            // Act
            var (width, height) = ThumbnailSize.Calculate(sourceWidth, sourceHeight, maxWidth, maxHeight);

            // Assert
            width.Should().Be(expectedWidth, "because the thumbnail must fit inside the bounding box");
            height.Should().Be(expectedHeight, "because the aspect ratio must be preserved");
        }

        [Test]
        public void Calculate_WhenSourceIsSmallerThanTheBox_LeavesItUnchanged()
        {
            // Arrange
            // Act
            var (width, height) = ThumbnailSize.Calculate(
                sourceWidth: 100,
                sourceHeight: 80,
                maxWidth: 480,
                maxHeight: 360);

            // Assert
            width.Should().Be(100, "because upscaling costs bytes and quality for no gain");
            height.Should().Be(80, "because a small source is used at its own size");
        }

        /// <summary>
        /// A very wide, very short source rounds to zero height, and an encoder handed a zero dimension throws.
        /// </summary>
        [Test]
        public void Calculate_ForAnExtremeAspectRatio_NeverReturnsZero()
        {
            // Arrange
            // Act
            var (width, height) = ThumbnailSize.Calculate(
                sourceWidth: 10_000,
                sourceHeight: 3,
                maxWidth: 480,
                maxHeight: 360);

            // Assert
            width.Should().BeGreaterThan(0, "because a zero dimension cannot be encoded");
            height.Should().BeGreaterThan(0, "because a zero dimension cannot be encoded");
        }

        [Test]
        public void Calculate_WithInvalidInput_Throws()
        {
            // Arrange
            // Act
            var act = () => ThumbnailSize.Calculate(
                sourceWidth: 0,
                sourceHeight: 100,
                maxWidth: 480,
                maxHeight: 360);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>(
                "because a zero-width source is a caller bug, not a value to guess around");
        }
    }
}
