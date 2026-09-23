using System.Text.Json;
using DlnaServer.Host.Configuration;

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

        private JsonElement? ReadSection()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_configPath));

            return document.RootElement.TryGetProperty("Dlna", out var section)
                ? section.Clone()
                : null;
        }
    }
}
