using DlnaServer.Core.Files;

namespace DlnaServer.UnitTests.Files
{
    /// <summary>
    /// Covers where a replaced file is put. Both kinds used to sit beside the original, which buried
    /// <c>config.json</c> and <c>dlna.sqlite</c> in a deployment folder an operator has to read.
    /// </summary>
    [TestFixture]
    internal sealed class BackupFilePathTest
    {
        private static readonly DateTime _whenUtc = new(2026, 9, 6, 14, 30, 0, DateTimeKind.Utc);

        private string _directory = null!;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"dlna-backup-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void CreateSaved_PutsTheCopyInTheBackupFolder()
        {
            // Arrange
            var original = Path.Combine(_directory, "config.json");

            // Act
            var actual = BackupFilePath.CreateSaved(original, _whenUtc);

            // Assert
            actual.Should().Be(
                Path.Combine(_directory, BackupFilePath.FolderName, "config.json.saved-2026-09-06_1430"),
                "because a settings backup belongs in the backup folder, not beside the file it copies");
        }

        [Test]
        public void CreateUnique_PutsTheMovedFileInTheBackupFolder()
        {
            // Arrange
            var original = Path.Combine(_directory, "dlna.sqlite");

            // Act
            var actual = BackupFilePath.CreateUnique(original, _whenUtc);

            // Assert
            actual.Should().Be(
                Path.Combine(_directory, BackupFilePath.FolderName, "dlna.sqlite.corrupt-2026-09-06_1430"),
                "because a database moved aside belongs in the backup folder too, and both kinds must "
                + "agree on where that is");
        }

        /// <summary>
        /// The folder has to exist before the caller moves or copies into it: <see cref="File.Move(string, string)"/>
        /// throws <see cref="DirectoryNotFoundException"/> rather than creating anything.
        /// </summary>
        [Test]
        public void CreateSaved_CreatesTheBackupFolder()
        {
            // Arrange
            var original = Path.Combine(_directory, "config.json");
            var expectedFolder = Path.Combine(_directory, BackupFilePath.FolderName);

            // Act
            _ = BackupFilePath.CreateSaved(original, _whenUtc);

            // Assert
            Directory.Exists(expectedFolder).Should().BeTrue(
                "because the caller copies straight into the returned path and File.Copy creates no "
                + "directories");
        }

        /// <summary>
        /// Minute-granularity stamps collide, and the second backup silently overwriting the first would
        /// destroy the very file it exists to preserve.
        /// </summary>
        [Test]
        public void CreateUnique_WhenTheNameIsTaken_NumbersTheNextOne()
        {
            // Arrange
            var original = Path.Combine(_directory, "dlna.sqlite");
            var first = BackupFilePath.CreateUnique(original, _whenUtc);
            File.WriteAllText(first, "taken");

            // Act
            var second = BackupFilePath.CreateUnique(original, _whenUtc);

            // Assert
            second.Should().Be($"{first}-2",
                "because two failures inside one minute must not resolve to the same file");
        }

        /// <summary>
        /// A settings backup deliberately does NOT get a numeric suffix - it undoes a mistaken edit, and
        /// an operator adjusting one field at a time would otherwise fill the folder in a sitting.
        /// </summary>
        [Test]
        public void CreateSaved_WhenTheNameIsTaken_ReturnsTheSamePath()
        {
            // Arrange
            var original = Path.Combine(_directory, "config.json");
            var first = BackupFilePath.CreateSaved(original, _whenUtc);
            File.WriteAllText(first, "taken");

            // Act
            var second = BackupFilePath.CreateSaved(original, _whenUtc);

            // Assert
            second.Should().Be(first,
                "because two saves inside one minute overwrite one another on purpose");
        }

        /// <summary>
        /// A bare file name resolves against the working directory, so its backup folder has to as well
        /// rather than landing at the filesystem root.
        /// </summary>
        [Test]
        public void CreateSaved_ForABareFileName_StaysRelative()
        {
            // Arrange
            var original = "config.json";

            // Act
            var actual = BackupFilePath.CreateSaved(original, _whenUtc);

            // Assert
            actual.Should().Be(
                Path.Combine(BackupFilePath.FolderName, "config.json.saved-2026-09-06_1430"),
                "because rooting it would write outside the deployment folder entirely");

            // The helper creates the folder, so clean up the one this case made in the working directory.
            if (Directory.Exists(BackupFilePath.FolderName))
            {
                Directory.Delete(BackupFilePath.FolderName, recursive: true);
            }
        }
    }
}
