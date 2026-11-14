using System.Text.Json;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;
using DlnaServer.Host.Configuration;
using Microsoft.Extensions.Configuration;

namespace DlnaServer.IntegrationTests
{
    [TestFixture]
    internal sealed class ConfigurationFileGuardTest
    {
        private string _directory = null!;
        private string _configPath = null!;

        [SetUp]
        public void SetUp()
        {
            _directory = Directory.CreateTempSubdirectory("dlna-config-").FullName;
            _configPath = Path.Combine(_directory, "config.json");
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_directory, recursive: true);
        }

        [Test]
        public void EnsureUsable_WhenFileIsMissing_WritesDefaults()
        {
            // Arrange
            // Act
            var result = ConfigurationFileGuard.EnsureUsable(_configPath, TimeProvider.System);

            // Assert
            result.Status.Should().Be(ConfigurationFileStatus.Created,
                "because a first run has no configuration file and must be given a usable one");
            File.Exists(_configPath).Should().BeTrue("because defaults were written");
            ReadSection().Should().NotBeNull("because the written file carries the Dlna section");
        }

        /// <summary>
        /// A recovered file used to carry an empty file-type map, and an empty map indexes nothing - so a
        /// server that lost its config.json had no media even after its folders were set again.
        /// </summary>
        /// <remarks>
        /// Read back through the configuration system rather than the raw JSON, because that is what the
        /// server sees - and it binds the written null profile as "", which every reader treats as unset.
        /// </remarks>
        [Test]
        public void EnsureUsable_WhenFileIsMissing_WritesTheShippedFileTypes()
        {
            // Arrange
            _ = ConfigurationFileGuard.EnsureUsable(_configPath, TimeProvider.System);

            // Act
            var bound = new ConfigurationBuilder()
                .AddJsonFile(_configPath, optional: false, reloadOnChange: false)
                .Build()
                .GetSection(DlnaOptions.SectionName)
                .Get<DlnaOptions>();

            // Assert
            var extensions = bound!.Library.MediaFileExtensions;

            extensions.Keys.Should().BeEquivalentTo(
                MediaFileExtensionDefaults.Create().Keys,
                "because a regenerated file must index the same file types the server ships with");

            extensions[".mkv"].Mime.Should().Be(nameof(DlnaMime.VideoXMatroska),
                "because the type is stored by its enum name, which is what the indexer parses");

            extensions.Values.Should().OnlyContain(static e => string.IsNullOrEmpty(e.ProfileName),
                "because the shipped map names no profile, so each type's own default applies");
        }

        [Test]
        public void EnsureUsable_WhenFileIsValid_LeavesItAlone()
        {
            // Arrange
            const string original = """{ "Dlna": { "Server": { "Port": 30000 } } }""";
            File.WriteAllText(_configPath, original);

            // Act
            var result = ConfigurationFileGuard.EnsureUsable(_configPath, TimeProvider.System);

            // Assert
            result.Status.Should().Be(ConfigurationFileStatus.Valid,
                "because a readable configuration file must never be rewritten");
            File.ReadAllText(_configPath).Should().Be(original,
                "because the user's settings must survive startup byte for byte");
        }

        /// <summary>
        /// The reference server's <c>config.json</c> is flat, so every key sits at the root. Dropped in
        /// here it parses, binds to nothing, and the server runs entirely on defaults looking healthy.
        /// </summary>
        [Test]
        public void EnsureUsable_WhenTheFileCarriesNoDlnaSection_SaysSoAndKeepsIt()
        {
            // Arrange
            const string original = """{ "ServerName": "DLNAServer", "ServerPort": 26851 }""";
            File.WriteAllText(_configPath, original);

            // Act
            var result = ConfigurationFileGuard.EnsureUsable(_configPath, TimeProvider.System);

            // Assert
            result.Status.Should().Be(ConfigurationFileStatus.Unrecognised,
                "because settings that bind to nothing are worth reporting, and are not the same failure "
                + "as a file that cannot be parsed");
            File.ReadAllText(_configPath).Should().Be(original,
                "because nothing is wrong with the file as a file - replacing it would destroy settings "
                + "the operator may only have mis-shaped");
        }

        [Test]
        public void EnsureUsable_WhenTheFileIsAnEmptyObject_IsStillValid()
        {
            // Arrange
            File.WriteAllText(_configPath, "{}");

            // Act
            var result = ConfigurationFileGuard.EnsureUsable(_configPath, TimeProvider.System);

            // Assert
            result.Status.Should().Be(ConfigurationFileStatus.Valid,
                "because an empty object carries no settings to lose, and the missing source folder it "
                + "produces is already reported on its own");
        }

        [TestCase("{ this is not json", TestName = "EnsureUsable_WhenFileIsMalformedJson_BacksUpAndReplaces")]
        [TestCase("", TestName = "EnsureUsable_WhenFileIsEmpty_BacksUpAndReplaces")]
        [TestCase("[1, 2, 3]", TestName = "EnsureUsable_WhenRootIsNotAnObject_BacksUpAndReplaces")]
        public void EnsureUsable_WhenFileIsUnusable_BacksUpAndReplaces(string content)
        {
            // Arrange
            File.WriteAllText(_configPath, content);

            // Act
            var result = ConfigurationFileGuard.EnsureUsable(_configPath, TimeProvider.System);

            // Assert
            result.Status.Should().Be(ConfigurationFileStatus.Replaced,
                "because an unreadable configuration file would otherwise kill the host with a parse error");
            result.BackupPath.Should().NotBeNull("because the original is kept so settings can be recovered");
            File.Exists(result.BackupPath!).Should().BeTrue("because the file was moved aside, not deleted");
            File.ReadAllText(result.BackupPath!).Should().Be(content,
                "because the backup must be the original content, untouched");
            result.Reason.Should().NotBeNullOrWhiteSpace("because the log has to say why it was rejected");
            ReadSection().Should().NotBeNull("because the replacement carries the Dlna section");
        }

        /// <summary>
        /// The configuration binder accepts values a strict deserializer rejects, so the guard must not
        /// condemn a file just because a value is typed loosely.
        /// </summary>
        [Test]
        public void EnsureUsable_WhenValuesAreLooselyTyped_KeepsTheFile()
        {
            // Arrange
            const string looselyTyped = """{ "Dlna": { "Server": { "Port": "26851", "DebugMode": "true" } } }""";
            File.WriteAllText(_configPath, looselyTyped);

            // Act
            var result = ConfigurationFileGuard.EnsureUsable(_configPath, TimeProvider.System);

            // Assert
            result.Status.Should().Be(ConfigurationFileStatus.Valid,
                "because a port written as a string binds fine - overwriting it would destroy a working config");
        }

        [Test]
        public void EnsureUsable_WhenReplacingTwice_KeepsBothBackups()
        {
            // Arrange
            File.WriteAllText(_configPath, "{ broken");
            var first = ConfigurationFileGuard.EnsureUsable(_configPath, TimeProvider.System);

            File.WriteAllText(_configPath, "{ broken again");

            // Act
            var second = ConfigurationFileGuard.EnsureUsable(_configPath, TimeProvider.System);

            // Assert
            first.BackupPath.Should().NotBe(second.BackupPath,
                "because a second failure must not silently overwrite the first backup");
            File.Exists(first.BackupPath!).Should().BeTrue("because earlier backups are retained");
            File.Exists(second.BackupPath!).Should().BeTrue("because the latest backup is retained");
        }

        /// <summary>
        /// A new container volume has no config.json, and defaults name no source folder - so the image
        /// served its own /app until the media folder was set somewhere.
        /// </summary>
        [Test]
        public void EnsureUsable_WhenFileIsMissingAndAnInitialFolderIsGiven_WritesThatFolder()
        {
            // Arrange
            // Act
            var result = ConfigurationFileGuard.EnsureUsable(
                filePath: _configPath,
                timeProvider: TimeProvider.System,
                initialSourceFolder: "/media");

            // Assert
            result.Status.Should().Be(ConfigurationFileStatus.Created,
                "because there was no file to keep");
            ReadSourceFolders().Should().Equal(["/media"],
                "because the first file written is the one place the initial folder is used");
        }

        /// <summary>
        /// The initial folder is a seed, not an override: once the file exists the Settings page owns it.
        /// </summary>
        [Test]
        public void EnsureUsable_WhenFileExistsAndAnInitialFolderIsGiven_LeavesItUnchanged()
        {
            // Arrange
            const string original = """{ "Dlna": { "Library": { "SourceFolders": [ "/media/Films" ] } } }""";
            File.WriteAllText(_configPath, original);

            // Act
            var result = ConfigurationFileGuard.EnsureUsable(
                filePath: _configPath,
                timeProvider: TimeProvider.System,
                initialSourceFolder: "/media");

            // Assert
            result.Status.Should().Be(ConfigurationFileStatus.Valid,
                "because a readable file is the operator's");
            File.ReadAllText(_configPath).Should().Be(original,
                "because a folder narrowed on the Settings page must survive every later start");
        }

        /// <summary>
        /// Only a missing file is seeded. A replaced one had the operator's own folders, which are recovered
        /// from its backup - not silently swapped for the image's media mount.
        /// </summary>
        [Test]
        public void EnsureUsable_WhenFileIsUnusableAndAnInitialFolderIsGiven_DoesNotWriteIt()
        {
            // Arrange
            File.WriteAllText(_configPath, "{ not json");

            // Act
            var result = ConfigurationFileGuard.EnsureUsable(
                filePath: _configPath,
                timeProvider: TimeProvider.System,
                initialSourceFolder: "/media");

            // Assert
            result.Status.Should().Be(ConfigurationFileStatus.Replaced,
                "because the file could not be parsed");
            ReadSourceFolders().Should().BeEmpty(
                "because the seed is for the first file only, and the replacement keeps the old defaults");
        }

        [Test]
        public void EnsureUsable_WhenFileIsMissingAndNoInitialFolderIsGiven_WritesNoFolder()
        {
            // Arrange
            // Act
            _ = ConfigurationFileGuard.EnsureUsable(
                filePath: _configPath,
                timeProvider: TimeProvider.System,
                initialSourceFolder: null);
            var bound = ReadBound()!;
            DlnaOptionsDefaults.Apply(bound);

            // Assert
            ReadSourceFolders().Should().BeEmpty(
                "because outside the image nothing names a folder, as before");
            bound.Library.SourceFolders.Should().Equal([AppContext.BaseDirectory],
                "because with no folder written the application-folder fallback still applies");
        }

        private string[] ReadSourceFolders()
        {
            var bound = ReadBound();

            return bound is null ? [] : [.. bound.Library.SourceFolders];
        }

        // Bound the way the server binds it, without the defaults applied.
        private DlnaOptions? ReadBound()
        {
            return new ConfigurationBuilder()
                .AddJsonFile(_configPath, optional: false, reloadOnChange: false)
                .Build()
                .GetSection(DlnaOptions.SectionName)
                .Get<DlnaOptions>();
        }

        private JsonElement? ReadSection()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_configPath));

            return document.RootElement.TryGetProperty("Dlna", out var section)
                ? section.Clone()
                : null;
        }
    }
}
