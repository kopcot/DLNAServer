using DlnaServer.Core.Configuration;
using DlnaServer.Host.Configuration;
using DlnaServer.Host.Diagnostics;

namespace DlnaServer.IntegrationTests
{
    [TestFixture]
    internal sealed class SubtitleFileCheckerTest
    {
        private string _root = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Directory.CreateTempSubdirectory("dlna-subtitles-").FullName;
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, recursive: true);
        }

        [Test]
        public void TryCheck_ForAnExistingFileTheRuleAllows_AcceptsIt()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_root, "film.en.srt"), "x");
            var checker = CreateChecker();

            // Act
            var isUsable = checker.TryCheck(
                mediaDirectory: _root,
                input: "film.en.srt",
                relativePath: out var relativePath,
                problem: out _);

            // Assert
            isUsable.Should().BeTrue("because the file exists beside the media and the rule allows it");
            relativePath.Should().Be("film.en.srt", "because the normalised path is what gets stored");
        }

        [Test]
        public void TryCheck_ForAMissingFile_RefusesItAndSaysWhy()
        {
            // Arrange
            var checker = CreateChecker();

            // Act
            var isUsable = checker.TryCheck(
                mediaDirectory: _root,
                input: "film.en.srt",
                relativePath: out _,
                problem: out var problem);

            // Assert
            isUsable.Should().BeFalse("because a path the rule allows still has to name a file on disc");
            problem.Should().NotBeEmpty("because the page has to tell the operator why it was refused");
        }

        /// <summary>
        /// A subtitle swapped for a link spells like an ordinary file, and would serve whatever it points at.
        /// </summary>
        [Test]
        public void TryCheck_ForASymbolicLink_RefusesIt()
        {
            // Arrange
            var target = Path.Combine(_root, "outside.txt");
            File.WriteAllText(target, "secret");
            CreateSymbolicLinkOrIgnore(Path.Combine(_root, "film.en.srt"), target);
            var checker = CreateChecker();

            // Act
            var isUsable = checker.TryCheck(
                mediaDirectory: _root,
                input: "film.en.srt",
                relativePath: out _,
                problem: out _);

            // Assert
            isUsable.Should().BeFalse("because a link can point anywhere, and the rule is about where the file is");
        }

        [Test]
        public void TryCheck_ForAFileInALinkedFolder_RefusesIt()
        {
            // Arrange
            var outside = Directory.CreateDirectory(Path.Combine(_root, "outside")).FullName;
            File.WriteAllText(Path.Combine(outside, "film.srt"), "x");
            var media = Directory.CreateDirectory(Path.Combine(_root, "media")).FullName;
            CreateSymbolicLinkOrIgnore(Path.Combine(media, "Subs"), outside, isDirectory: true);
            var checker = CreateChecker();

            // Act
            var isUsable = checker.TryCheck(
                mediaDirectory: media,
                input: "Subs/film.srt",
                relativePath: out _,
                problem: out _);

            // Assert
            isUsable.Should().BeFalse("because a linked sub-folder leads out of the media folder as surely as a linked file");
        }

        private static SubtitleFileChecker CreateChecker()
        {
            var options = new StaticOptionsMonitor<DlnaOptions>(new DlnaOptions());

            return new SubtitleFileChecker(new TemporaryFolderVisibility(options, TimeProvider.System));
        }

        private static void CreateSymbolicLinkOrIgnore(string path, string target, bool isDirectory = false)
        {
            try
            {
                _ = isDirectory
                    ? Directory.CreateSymbolicLink(path, target)
                    : File.CreateSymbolicLink(path, target);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Windows allows links only to an administrator or in developer mode.
                Assert.Ignore($"Symbolic links cannot be created here: {exception.Message}");
            }
        }
    }
}
