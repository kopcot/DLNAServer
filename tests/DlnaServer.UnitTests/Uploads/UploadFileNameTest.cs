using DlnaServer.Core.Configuration;
using DlnaServer.Core.Uploads;

namespace DlnaServer.UnitTests.Uploads
{
    /// <summary>
    /// Covers what a browser is allowed to call a file, and which files are worth taking at all.
    /// </summary>
    [TestFixture]
    internal sealed class UploadFileNameTest
    {
        [TestCase("Holiday/DSC_0001.jpg", "DSC_0001.jpg")]
        [TestCase("Holiday\\DSC_0001.jpg", "DSC_0001.jpg")]
        [TestCase("C:\\Users\\someone\\DSC_0001.jpg", "DSC_0001.jpg")]
        [TestCase("  film.mp4  ", "film.mp4")]
        public void Sanitise_KeepsOnlyTheLastSegment(string sent, string expected)
        {
            // Arrange
            // Act
            var name = UploadFileName.Sanitise(sent);

            // Assert
            name.Should().Be(expected,
                "because a directory upload sends a path with the name and an old browser sends the whole "
                + "local one - and a name with no separator left in it has nowhere to walk to");
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(".")]
        [TestCase("..")]
        [TestCase("Films/")]
        public void Sanitise_WithNothingUsableLeft_ReturnsEmpty(string sent)
        {
            // Arrange
            // Act
            var name = UploadFileName.Sanitise(sent);

            // Assert
            name.Should().BeEmpty(
                "because the caller refuses the file rather than inventing a name for it, and '.' and "
                + "'..' name folders rather than files");
        }

        [TestCase("film\r\nupload from=10.0.0.1.mp4")]
        [TestCase("film\n.mp4")]
        [TestCase("film\t.mp4")]
        [TestCase("film\u001b[31m.mp4")]
        public void Sanitise_WithAControlCharacter_ReturnsEmpty(string sent)
        {
            // Arrange
            // Act
            var name = UploadFileName.Sanitise(sent);

            // Assert
            name.Should().BeEmpty(
                "because Linux accepts a line break in a file name, and one sent through filename*= would "
                + "otherwise reach the disc, the report and the security log");
        }

        [Test]
        public void IsAcceptedMedia_ForAnExtensionInTheCatalog_IsTrue()
        {
            // Arrange
            var library = new LibraryOptions();

            // Act
            var accepted = UploadFileName.IsAcceptedMedia(library, "film.mp4");

            // Assert
            accepted.Should().BeTrue(
                "because the catalog is what the scanner falls back to, so anything it recognises as "
                + "presentable media would be indexed once it landed");
        }

        [Test]
        public void IsAcceptedMedia_ForASubtitleSidecar_IsFalse()
        {
            // Arrange
            var library = new LibraryOptions();

            // Act
            var accepted = UploadFileName.IsAcceptedMedia(library, "film.srt");

            // Assert
            accepted.Should().BeFalse(
                "because a subtitle file is not a library item - the scanner would walk past it, so "
                + "accepting one would put a file in the media folders that nothing ever shows");
        }

        [Test]
        public void IsAcceptedMedia_ForAConfiguredExtension_IsTrue()
        {
            // Arrange
            var library = new LibraryOptions();
            library.MediaFileExtensions[".weird"] = new MediaExtensionOptions { Mime = "VideoMp4" };

            // Act
            var accepted = UploadFileName.IsAcceptedMedia(library, "film.weird");

            // Assert
            accepted.Should().BeTrue(
                "because configuration wins outright over the catalog, which is what lets an operator "
                + "teach this server an extension their television understands");
        }

        [Test]
        public void IsAcceptedMedia_ForAConfiguredExtensionWithANonsenseMime_IsFalse()
        {
            // Arrange
            var library = new LibraryOptions();
            library.MediaFileExtensions[".weird"] = new MediaExtensionOptions { Mime = "999" };

            // Act
            var accepted = UploadFileName.IsAcceptedMedia(library, "film.weird");

            // Assert
            accepted.Should().BeFalse(
                "because Enum.TryParse accepts a numeric string, so '999' would otherwise pass as a MIME "
                + "no profile or media kind maps from - the same trap the indexer guards against");
        }

        [Test]
        public void IsAcceptedMedia_WithNoExtension_IsFalse()
        {
            // Arrange
            var library = new LibraryOptions();

            // Act
            var accepted = UploadFileName.IsAcceptedMedia(library, "README");

            // Assert
            accepted.Should().BeFalse("because nothing can be resolved from a name with no extension");
        }
    }
}
