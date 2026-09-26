using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;
using DlnaServer.Host.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the write half of <c>config.json</c>, which had no tests at all.
    /// </summary>
    /// <remarks>
    /// It owns the write-temporary-then-move dance that the configuration provider's own file watcher is
    /// racing against, so a defect here is read straight back by the reload it triggers - the same seam
    /// the last-good provider exists to survive.
    /// </remarks>
    [TestFixture]
    internal sealed class SettingsWriterTest
    {
        private string _root = null!;
        private string _configurationPath = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Directory.CreateTempSubdirectory("dlna-settings-").FullName;
            _configurationPath = Path.Combine(_root, "config.json");
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, recursive: true);
        }

        private SettingsWriter CreateWriter()
        {
            return new SettingsWriter(
                _configurationPath,
                TimeProvider.System,
                NullLogger<SettingsWriter>.Instance);
        }

        [Test]
        public async Task SaveAsync_KeepsSectionsItDoesNotOwn()
        {
            // Arrange
            await File.WriteAllTextAsync(_configurationPath, """
                {
                  "Serilog": { "MinimumLevel": "Warning" },
                  "Dlna": { "Server": { "Port": 1 } }
                }
                """);

            var writer = CreateWriter();
            var options = new DlnaOptions();
            options.Server.Port = 26852;

            // Act
            await writer.SaveAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            var written = new ConfigurationBuilder().AddJsonFile(_configurationPath).Build();

            written["Serilog:MinimumLevel"].Should().Be("Warning",
                "because a deployment keeps its own settings in this file and an edit made through the "
                + "admin UI must not discard them");
            written["Dlna:Server:Port"].Should().Be("26852",
                "because the section the writer does own has to be replaced by what was saved");
        }

        /// <summary>
        /// The regression behind the parse-options fix: the writer read with stricter JSON rules than
        /// the configuration system it writes for.
        /// </summary>
        /// <remarks>
        /// A comment made <c>JsonNode.Parse</c> throw, which was caught and answered with an empty
        /// object - so saving one setting through the admin UI silently deleted every other section of
        /// a file the server itself reads quite happily.
        /// </remarks>
        [Test]
        public async Task SaveAsync_WhenTheFileCarriesCommentsAndTrailingCommas_KeepsTheOtherSections()
        {
            // Arrange
            await File.WriteAllTextAsync(_configurationPath, """
                {
                  // the operator's own note, written over SSH
                  "Serilog": { "MinimumLevel": "Warning" },
                  "Dlna": { "Server": { "Port": 1 } },
                }
                """);

            var writer = CreateWriter();
            var options = new DlnaOptions();
            options.Server.Port = 26852;

            // Act
            await writer.SaveAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            var written = new ConfigurationBuilder().AddJsonFile(_configurationPath).Build();

            written["Serilog:MinimumLevel"].Should().Be("Warning",
                "because the configuration system accepts comments and a trailing comma, so anything "
                + "that rewrites the file has to accept them too rather than start from an empty object");
            written["Dlna:Server:Port"].Should().Be("26852",
                "because the save still has to happen");
        }

        /// <summary>
        /// The whole round trip a Settings save makes: written by the page, read back by the binder, put in
        /// shape by the defaults - with a type removed staying removed and none of the shipped ones returning.
        /// </summary>
        [Test]
        public async Task SaveAsync_SubtitleTypes_ReadBackAsSavedWithTheirKindsByName()
        {
            // Arrange
            var writer = CreateWriter();
            var options = new DlnaOptions();
            options.Library.SubtitleFileExtensions = new Dictionary<string, DlnaMedia>(StringComparer.OrdinalIgnoreCase)
            {
                [".txt"] = DlnaMedia.Video,
                [".lrc"] = DlnaMedia.Audio,
            };

            // Act
            await writer.SaveAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            var written = new ConfigurationBuilder().AddJsonFile(_configurationPath).Build();
            var reread = new DlnaOptions();
            written.GetSection(DlnaOptions.SectionName).Bind(reread);
            DlnaOptionsDefaults.Apply(reread);

            written["Dlna:Library:SubtitleFileExtensions:.lrc"].Should().Be("Audio",
                "because the file is edited by hand, so a kind is written as its name rather than a number");
            reread.Library.SubtitleFileExtensions.Should().BeEquivalentTo(
                new Dictionary<string, DlnaMedia> { [".txt"] = DlnaMedia.Video, [".lrc"] = DlnaMedia.Audio },
                "because what the Settings page saved is exactly what the server reads back, .srt and the rest not returning");
        }

        [Test]
        public async Task SaveAsync_WhenTheFileDoesNotExist_WritesOne()
        {
            // Arrange
            var writer = CreateWriter();
            var options = new DlnaOptions();
            options.Server.Port = 26852;

            // Act
            await writer.SaveAsync(options, cancellationToken: CancellationToken.None);

            // Assert
            File.Exists(_configurationPath).Should().BeTrue(
                "because a first save on a deployment that has no file yet must produce one");

            var written = new ConfigurationBuilder().AddJsonFile(_configurationPath).Build();

            written["Dlna:Server:Port"].Should().Be("26852",
                "because the file it produced has to be one the configuration system can read back");
        }

        /// <summary>
        /// The temporary file must never be left behind, because the provider watches this directory.
        /// </summary>
        [Test]
        public async Task SaveAsync_LeavesNoTemporaryFileBehind()
        {
            // Arrange
            var writer = CreateWriter();

            // Act
            await writer.SaveAsync(new DlnaOptions(), cancellationToken: CancellationToken.None);

            // Assert
            File.Exists(_configurationPath + ".saving").Should().BeFalse(
                "because the write is published by moving the temporary over the real path, and a "
                + "leftover would be a half-written file sitting next to the one being watched");
        }

        /// <summary>
        /// Concurrent saves are serialised, so the shared temporary path cannot be published half-built.
        /// </summary>
        /// <remarks>
        /// Both circuits of the admin UI reach one singleton, and the read-mutate-write had no lock -
        /// two saves could interleave onto the same <c>.saving</c> file and move a partial one into
        /// place, which the configuration provider is watching for and would read.
        /// </remarks>
        [Test]
        public async Task SaveAsync_WhenTwoSavesOverlap_StillLeavesAReadableFile()
        {
            // Arrange
            var writer = CreateWriter();

            var first = new DlnaOptions();
            first.Server.Port = 26852;

            var second = new DlnaOptions();
            second.Server.Port = 26862;

            // Act
            await Task.WhenAll(
                writer.SaveAsync(first, cancellationToken: CancellationToken.None),
                writer.SaveAsync(second, cancellationToken: CancellationToken.None));

            // Assert
            var written = new ConfigurationBuilder().AddJsonFile(_configurationPath).Build();

            written["Dlna:Server:Port"].Should().BeOneOf(["26852", "26862"],
                "because either save may win, but the file must be one of them in full rather than a "
                + "blend of the two");
        }
    }
}
