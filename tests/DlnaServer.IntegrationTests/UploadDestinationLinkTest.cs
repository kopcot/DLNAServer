using DlnaServer.Core.Configuration;
using DlnaServer.Core.Uploads;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the half of the upload containment check that has to look at the disc.
    /// </summary>
    /// <remarks>
    /// The sibling unit fixture works on strings alone, which is why it could not see this: the guards
    /// there are lexical, and a symlink spells exactly like an ordinary folder. These cases need real
    /// directories and a real link, so they live here.
    /// </remarks>
    [TestFixture]
    internal sealed class UploadDestinationLinkTest
    {
        private string _root = null!;
        private string _outside = null!;

        [SetUp]
        public void SetUp()
        {
            var temporaryFolder = Path.GetFullPath(Path.GetTempPath());

            _root = Path.Combine(temporaryFolder, $"dlna-upload-root-{Guid.NewGuid():N}");
            _outside = Path.Combine(temporaryFolder, $"dlna-upload-outside-{Guid.NewGuid():N}");

            _ = Directory.CreateDirectory(_root);
            _ = Directory.CreateDirectory(_outside);
        }

        [TearDown]
        public void TearDown()
        {
            Remove(_root);
            Remove(_outside);
        }

        [Test]
        public void TryResolve_WithASubFolderThatIsALink_IsRefused()
        {
            // Arrange
            var options = Options();
            var link = Path.Combine(_root, "elsewhere");

            CreateLinkOrIgnore(link, _outside);

            // Act
            var resolved = UploadDestination.TryResolve(options, _root, "elsewhere", out _, out var problem);

            // Assert
            resolved.Should().BeFalse(
                "because the link leaves the folder the operator allowed, and Path.GetFullPath cannot see "
                + "that it does");
            problem.Should().NotBeEmpty("because the page has to be able to say why the upload was refused");

            // Pins the reason this fixture exists: the lexical containment test still passes for this
            // path, so it is the filesystem check above - and only that - refusing the upload.
            Path.GetFullPath(link).Should().StartWith(_root + Path.DirectorySeparatorChar,
                "because a link spells like any other segment, which is what made the string test alone "
                + "insufficient");
        }

        [Test]
        public void TryResolve_WithALinkFurtherDownThePath_IsRefused()
        {
            // Arrange
            var options = Options();
            var branch = Path.Combine(_root, "Films");
            var link = Path.Combine(branch, "elsewhere");

            _ = Directory.CreateDirectory(branch);
            CreateLinkOrIgnore(link, _outside);

            // Act
            var resolved = UploadDestination.TryResolve(
                options,
                _root,
                "Films/elsewhere/2026",
                out _,
                out var problem);

            // Assert
            resolved.Should().BeFalse(
                "because a link anywhere along the path escapes the tree, not only one named as the last "
                + "segment");
            problem.Should().NotBeEmpty("because the page has to be able to say why the upload was refused");
        }

        [Test]
        public void TryResolve_WithAnOrdinarySubFolder_IsAllowed()
        {
            // Arrange
            var options = Options();

            _ = Directory.CreateDirectory(Path.Combine(_root, "Films"));

            // Act
            var resolved = UploadDestination.TryResolve(options, _root, "Films/2026", out var fullPath, out _);

            // Assert
            resolved.Should().BeTrue(
                "because refusing links must not also refuse the ordinary folders uploading exists for");
            fullPath.Should().Be(Path.Combine(_root, "Films", "2026"),
                "because a folder that does not exist yet is still a legitimate destination - the endpoint "
                + "creates it");
        }

        /// <summary>
        /// A root such as <c>/</c> or <c>C:\</c> keeps its separator, and the walk used to skip one
        /// character past it - so the first folder was probed misspelled, never found, and never checked.
        /// </summary>
        /// <remarks>
        /// Uses a link the operating system already ships on its root, because a test cannot create one
        /// there: <c>C:\Documents and Settings</c> on Windows, and the merged-<c>/usr</c> links on Linux.
        /// </remarks>
        [Test]
        public void TryResolve_UnderARootWhoseFirstFolderIsALink_IsRefused()
        {
            // Arrange
            var root = FileSystemRoot();
            var link = ExistingLinkOnTheRootOrIgnore(root);
            var options = RootOptions(root);

            // Act
            var resolved = UploadDestination.TryResolve(options, root, $"{link}/Uploads", out _, out var problem);

            // Assert
            resolved.Should().BeFalse(
                "because the first folder under a root is a link, and a root source folder must "
                + "not be the one place the link check is skipped");
            problem.Should().NotBeEmpty("because the page has to be able to say why the upload was refused");
        }

        [Test]
        public void TryResolve_UnderARootThroughOrdinaryFolders_IsAllowed()
        {
            // Arrange
            var root = FileSystemRoot();
            var folder = OperatingSystem.IsWindows()
                ? "Windows"
                : "etc";
            var options = RootOptions(root);

            // Act
            var resolved = UploadDestination.TryResolve(
                options,
                root,
                $"{folder}/dlna-not-there-{Guid.NewGuid():N}",
                out _,
                out var problem);

            // Assert
            resolved.Should().BeTrue(
                "because refusing links under a root must not also refuse the real folders on it; problem: {0}",
                problem);
        }

        private static string FileSystemRoot()
        {
            return OperatingSystem.IsWindows()
                ? Path.GetPathRoot(Environment.SystemDirectory)!
                : "/";
        }

        private static string ExistingLinkOnTheRootOrIgnore(string root)
        {
            string[] candidates = OperatingSystem.IsWindows()
                ? ["Documents and Settings"]
                : ["bin", "sbin", "lib", "lib64"];

            foreach (var candidate in candidates)
            {
                var folder = new DirectoryInfo(Path.Combine(root, candidate));

                if (folder.Exists && folder.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return candidate;
                }
            }

            Assert.Ignore($"No link to test with was found on '{root}'.");

            return string.Empty;
        }

        private static DlnaOptions RootOptions(string root)
        {
            var options = new DlnaOptions();

            options.Library.SourceFolders = [root];
            options.Library.ExcludeFolders = [];

            return options;
        }

        private static void CreateLinkOrIgnore(string path, string target)
        {
            try
            {
                _ = Directory.CreateSymbolicLink(path, target);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Windows refuses a symlink without developer mode or elevation. Linux is what the server
                // is deployed on, so the case is still covered where it matters.
                Assert.Ignore($"This machine does not allow creating a symbolic link: {exception.Message}");
            }
        }

        private static void Remove(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A leftover temporary folder must not turn into a second, misleading test failure.
            }
        }

        private DlnaOptions Options()
        {
            var options = new DlnaOptions();

            options.Library.SourceFolders = [_root];
            options.Library.ExcludeFolders = [];

            return options;
        }
    }
}
