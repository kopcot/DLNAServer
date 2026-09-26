using DlnaServer.Core.Files;

namespace DlnaServer.UnitTests.Files
{
    [TestFixture]
    internal sealed class PathSegmentsTest
    {
        [TestCase(".")]
        [TestCase("..")]
        [TestCase("Films/../Private")]
        [TestCase("Films\\.\\Private")]
        [TestCase("/media/Films/..")]
        public void HasDotSegment_WithADotSegment_IsTrue(string entry)
        {
            // Arrange
            // Act
            var hasDotSegment = PathSegments.HasDotSegment(entry);

            // Assert
            hasDotSegment.Should().BeTrue(
                "because '.' or '..' as a whole segment, between either separator, changes which folder the "
                + "path names");
        }

        [TestCase("")]
        [TestCase("Films/Private")]
        [TestCase(".@__thumb")]
        [TestCase("Films/...")]
        [TestCase("Films/..hidden")]
        public void HasDotSegment_WithoutADotSegment_IsFalse(string entry)
        {
            // Arrange
            // Act
            var hasDotSegment = PathSegments.HasDotSegment(entry);

            // Assert
            hasDotSegment.Should().BeFalse(
                "because a dot inside a longer segment is part of an ordinary folder name");
        }
    }
}
