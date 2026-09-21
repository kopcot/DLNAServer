using DlnaServer.Core.Configuration;
using DlnaServer.Core.Uploads;

namespace DlnaServer.UnitTests.Uploads
{
    /// <summary>
    /// Covers the folders offered being the same strings everything downstream records.
    /// </summary>
    /// <remarks>
    /// Its own fixture because the failure it guards is not about refusing anything: every check passed,
    /// the upload worked, and the only symptom was that the destination was never offered again. The page
    /// showed the root as configured, the endpoint wrote to the resolved path, and the two were compared
    /// against each other.
    /// </remarks>
    [TestFixture]
    internal sealed class UploadRootsTest
    {
        [Test]
        public void Roots_WithARelativeSourceFolder_AnswersAFullPath()
        {
            // Arrange
            var options = new DlnaOptions();
            options.Library.SourceFolders = ["media"];

            // Act
            var roots = UploadDestination.Roots(options);

            // Assert
            roots.Should().ContainSingle("because one source folder was configured")
                .Which.Should().Be(Path.GetFullPath("media"),
                    "because the destination recorded against a device is a full path, so a root offered "
                    + "in any other spelling could never be matched back to it");
        }

        [Test]
        public void Roots_WithATrailingSeparator_AnswersWithoutIt()
        {
            // Arrange
            var options = new DlnaOptions();
            options.Library.SourceFolders = [Path.GetTempPath()];

            // Act
            var roots = UploadDestination.Roots(options);

            // Assert
            roots.Should().ContainSingle().Which.Should().Be(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                "because one spelling has to win, and a trailing slash is the difference most likely to "
                + "arrive from a configuration file someone typed by hand");
        }

        [Test]
        public void Roots_WithAnUnusablePath_LeavesItOut()
        {
            // Arrange
            var options = new DlnaOptions();
            // A null character, because .NET 8 stopped rejecting most of what used to be an invalid path
            // character - this is one of the few inputs Path.GetFullPath still refuses outright.
            options.Library.SourceFolders = ["media\0broken", Path.GetTempPath()];

            // Act
            var roots = UploadDestination.Roots(options);

            // Assert
            roots.Should().ContainSingle(
                "because a path too malformed to resolve cannot be offered, and throwing here would take "
                + "out the page rather than the one bad entry - which validation reports separately");
        }

        [Test]
        public void TryResolve_WithTheRootAsTheOperatorTypedIt_StillResolves()
        {
            // Arrange
            var options = new DlnaOptions();
            options.Library.SourceFolders = ["media"];

            // Act
            var resolved = UploadDestination.TryResolve(options, "media", subFolder: null, out var path, out _);

            // Assert
            resolved.Should().BeTrue(
                "because the form may post the root in the configured spelling, and both sides are "
                + "resolved before they are compared");
            path.Should().Be(Path.GetFullPath("media"), "because what is written to is the resolved path");
        }
    }
}
