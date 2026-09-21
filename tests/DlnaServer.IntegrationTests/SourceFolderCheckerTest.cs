using DlnaServer.Host.Configuration;

namespace DlnaServer.IntegrationTests
{
    [TestFixture]
    internal sealed class SourceFolderCheckerTest
    {
        private string _root = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"dlna-sources-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Test]
        public void Check_WithAnExistingFolder_ReportsItUsable()
        {
            // Arrange
            var checker = new SourceFolderChecker();

            // Act
            var results = checker.Check([_root]);

            // Assert
            results.Should().ContainSingle("because one path was checked")
                .Which.Problem.Should().BeNull(
                    "because a folder that exists and can be read has nothing wrong with it");
        }

        /// <summary>
        /// An unmounted QNAP volume leaves its mountpoint behind as an existing, readable, EMPTY
        /// directory - so every other check passes and reconciliation reads "every file is gone".
        /// Reported separately from a problem, because an empty folder is a legitimate thing to configure.
        /// </summary>
        [Test]
        public void Check_WithAnEmptyFolder_ReportsItEmptyButNotBroken()
        {
            // Arrange
            var checker = new SourceFolderChecker();

            // Act
            var results = checker.Check([_root]);

            // Assert
            var result = results.Should().ContainSingle("because one path was checked").Which;

            result.Problem.Should().BeNull(
                "because an empty folder is a perfectly good thing to configure - the operator may be "
                + "about to fill it - so the admin UI must not call it broken");
            result.IsEmpty.Should().BeTrue(
                "because reconciliation needs to know, even though the admin UI does not care");
            result.IsSafeToReconcile.Should().BeFalse(
                "because deleting every indexed row under a folder that only LOOKS empty is what an "
                + "unmounted share caused");
        }

        [Test]
        public void Check_WithAPopulatedFolder_ReportsItSafeToReconcile()
        {
            // Arrange
            var checker = new SourceFolderChecker();
            File.WriteAllText(Path.Combine(_root, "film.mkv"), "x");

            // Act
            var results = checker.Check([_root]);

            // Assert
            var result = results.Should().ContainSingle("because one path was checked").Which;

            result.IsEmpty.Should().BeFalse("because the folder holds an entry");
            result.IsSafeToReconcile.Should().BeTrue(
                "because a readable folder with content is the only state in which removing rows is safe");
        }

        [Test]
        public void Check_WithAMissingFolder_ReportsItMissing()
        {
            // Arrange
            var checker = new SourceFolderChecker();
            var missing = Path.Combine(_root, "not-here");

            // Act
            var results = checker.Check([missing]);

            // Assert
            results[0].IsUsable.Should().BeFalse(
                "because a source folder that does not exist cannot be served");
            results[0].Problem.Should().Be("No such folder.",
                "because the operator needs to be told which of the failures this is");
        }

        /// <summary>
        /// A path naming a file is the mistake a folder-shaped field invites, and it is not the same
        /// failure as a missing folder.
        /// </summary>
        [Test]
        public void Check_WithAFileInsteadOfAFolder_SaysSo()
        {
            // Arrange
            var checker = new SourceFolderChecker();
            var file = Path.Combine(_root, "movie.mkv");
            File.WriteAllText(file, "not a folder");

            // Act
            var results = checker.Check([file]);

            // Assert
            results[0].Problem.Should().Be("This is a file, not a folder.",
                "because reporting a file as a missing folder would send the operator looking for the wrong thing");
        }

        /// <summary>
        /// A relative path resolves against the working directory, which differs between a hand-started
        /// server and one the NAS starts - so it is reported even though something exists there.
        /// </summary>
        [Test]
        public void Check_WithARelativePath_ReportsItEvenWhenItResolves()
        {
            // Arrange
            var checker = new SourceFolderChecker();

            // Act
            var results = checker.Check(["."]);

            // Assert
            results[0].IsUsable.Should().BeFalse(
                "because the current directory exists but is not a stable place to serve media from");
            results[0].Problem.Should().Contain("absolute",
                "because the problem is the shape of the path, not whether anything is there");
        }

        [Test]
        public void Check_WithSeveralPaths_ReportsEveryOneInOrder()
        {
            // Arrange
            var checker = new SourceFolderChecker();
            var missing = Path.Combine(_root, "not-here");

            // Act
            var results = checker.Check([missing, _root]);

            // Assert
            results.Should().HaveCount(2,
                "because every path is reported, not just the first failure");
            results[0].Path.Should().Be(missing,
                "because a result has to be matchable back to the line it came from");
            results[1].IsUsable.Should().BeTrue(
                "because a bad path earlier in the list must not stop the rest being checked");
        }

        [Test]
        public void Check_WithABlankLine_ReportsItRatherThanThrowing()
        {
            // Arrange
            var checker = new SourceFolderChecker();

            // Act
            var results = checker.Check(["   "]);

            // Assert
            results[0].Problem.Should().Be("Blank.",
                "because whitespace reaching the checker is a typo to report, not an exception to raise");
        }
    }
}
