using DlnaServer.Core.Configuration;
using DlnaServer.Core.Uploads;

namespace DlnaServer.UnitTests.Uploads
{
    /// <summary>
    /// Covers the only thing standing between a posted form and an arbitrary write on this machine.
    /// </summary>
    /// <remarks>
    /// The form names its destination, and a form field is whatever the sender decides to put in it - so
    /// every case below is written from the attacker's side rather than the operator's: what happens when
    /// the root is not one that was offered, when the sub-folder climbs out of it, when it is absolute,
    /// and when it aims at a folder the library would never look in.
    /// </remarks>
    [TestFixture]
    internal sealed class UploadDestinationTest
    {
        /// <summary>
        /// Full paths, because that is what <see cref="UploadDestination.Roots"/> answers with - the
        /// temporary folder is a short 8.3 path on some Windows accounts, and comparing a resolved path
        /// against an unresolved one is the bug these two constants would otherwise hide.
        /// </summary>
        private static readonly string _root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "dlna-upload-root"));
        private static readonly string _otherRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "dlna-upload-other"));

        [Test]
        public void Roots_WithNoPinnedFolder_OffersEverySourceFolder()
        {
            // Arrange
            var options = Options();

            // Act
            var roots = UploadDestination.Roots(options);

            // Assert
            roots.Should().BeEquivalentTo([_root, _otherRoot],
                "because with nothing pinned the operator may send files to any folder the library reads");
        }

        [Test]
        public void Roots_WithAPinnedFolder_OffersOnlyThatOne()
        {
            // Arrange
            var options = Options();
            options.Upload.DestinationFolder = _root;

            // Act
            var roots = UploadDestination.Roots(options);

            // Assert
            roots.Should().BeEquivalentTo([_root],
                "because pinning a folder is the setting that exists to stop uploads reaching the rest "
                + "of the library");
        }

        [Test]
        public void TryResolve_WithASubFolder_LandsUnderTheRoot()
        {
            // Arrange
            var options = Options();

            // Act
            var resolved = UploadDestination.TryResolve(options, _root, "Films/2026", out var path, out _);

            // Assert
            resolved.Should().BeTrue("because a plain relative folder under a source folder is the ordinary case");
            path.Should().Be(Path.Combine(_root, "Films", "2026"),
                "because the sub-folder is combined with the root and normalised to this platform's separator");
        }

        [Test]
        public void TryResolve_WithNoSubFolder_LandsInTheRootItself()
        {
            // Arrange
            var options = Options();

            // Act
            var resolved = UploadDestination.TryResolve(options, _root, subFolder: null, out var path, out _);

            // Assert
            resolved.Should().BeTrue("because the sub-folder is optional");
            path.Should().Be(_root, "because an upload with no sub-folder goes to the folder itself");
        }

        /// <summary>
        /// The one that matters: a root nobody offered must be refused even though it is a real folder.
        /// </summary>
        [Test]
        public void TryResolve_WithARootThatWasNotOffered_IsRefused()
        {
            // Arrange
            var options = Options();
            options.Upload.DestinationFolder = _root;

            // Act
            var resolved = UploadDestination.TryResolve(options, _otherRoot, subFolder: null, out _, out var problem);

            // Assert
            resolved.Should().BeFalse(
                "because the endpoint re-derives the allowed roots from configuration - a form saying "
                + "otherwise is exactly what pinning a folder is meant to stop");
            problem.Should().NotBeEmpty("because the operator is told why the upload was refused");
        }

        [TestCase("..")]
        [TestCase("../..")]
        [TestCase("Films/../../elsewhere")]
        [TestCase("Films/./2026")]
        public void TryResolve_WithADotSegment_IsRefused(string subFolder)
        {
            // Arrange
            var options = Options();

            // Act
            var resolved = UploadDestination.TryResolve(options, _root, subFolder, out _, out var problem);

            // Assert
            resolved.Should().BeFalse(
                "because '..' is how a relative path climbs out of the folder it was given, and a write "
                + "outside the library is the whole risk this feature carries");
            problem.Should().NotBeEmpty("because a refusal always says what was wrong with the choice");
        }

        [Test]
        public void TryResolve_WithAnAbsoluteSubFolder_IsRefused()
        {
            // Arrange
            var options = Options();
            var absolute = Path.Combine(Path.GetTempPath(), "somewhere-else");

            // Act
            var resolved = UploadDestination.TryResolve(options, _root, absolute, out _, out _);

            // Assert
            resolved.Should().BeFalse(
                "because Path.Combine DISCARDS the root when the second part is rooted, so an absolute "
                + "sub-folder would silently become the destination rather than a folder inside it");
        }

        [Test]
        public void TryResolve_IntoAnExcludedFolder_IsRefused()
        {
            // Arrange
            var options = Options();
            options.Library.ExcludeFolders = [".@__thumb"];

            // Act
            var resolved = UploadDestination.TryResolve(options, _root, ".@__thumb", out _, out var problem);

            // Assert
            resolved.Should().BeFalse(
                "because the scanner never descends into an excluded folder, so a file written there "
                + "would be accepted and then never appear - which reads as data loss, not as a setting");
            problem.Should().NotBeEmpty("because that refusal is the one most likely to look like a bug");
        }

        [Test]
        public void TryResolve_WithNoRootChosen_IsRefused()
        {
            // Arrange
            var options = Options();

            // Act
            var resolved = UploadDestination.TryResolve(options, root: null, subFolder: null, out _, out var problem);

            // Assert
            resolved.Should().BeFalse("because a post that names no destination cannot be guessed at");
            problem.Should().NotBeEmpty("because the page has to be able to say what is missing");
        }

        private static DlnaOptions Options()
        {
            var options = new DlnaOptions();

            options.Library.SourceFolders = [_root, _otherRoot];
            options.Library.ExcludeFolders = [];

            return options;
        }
    }
}
