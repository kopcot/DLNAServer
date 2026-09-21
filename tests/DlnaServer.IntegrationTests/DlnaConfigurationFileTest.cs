using DlnaServer.Core.Configuration;
using DlnaServer.Host.Configuration;
using Microsoft.Extensions.Configuration;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the two halves of registering <c>config.json</c>, both of which were wrong at once and
    /// produced a server that started cleanly on the wrong port serving the wrong folder.
    /// </summary>
    /// <remarks>
    /// The values asserted here are the ones whose absence was visible in the deployment log: the port,
    /// which fell back to the reference implementation's 26851 and collided with it, and the source
    /// folders, whose absence triggered the serve-my-own-folder fallback.
    /// </remarks>
    [TestFixture]
    internal sealed class DlnaConfigurationFileTest
    {
        private string _root = null!;
        private string _configurationPath = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Directory.CreateTempSubdirectory("dlna-config-").FullName;
            _configurationPath = Path.Combine(_root, "config.json");
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, recursive: true);
        }

        /// <summary>
        /// The regression. The path handed in is absolute, and the
        /// <c>Action&lt;JsonConfigurationSource&gt;</c> overload does not resolve a file provider for it
        /// the way the <c>(path, optional, reloadOnChange)</c> overload does - so without an explicit
        /// <c>ResolveFileProvider()</c> the file is silently not found and every value falls back to its
        /// code default.
        /// </summary>
        [Test]
        public void Add_WithAnAbsolutePath_ActuallyReadsTheFile()
        {
            // Arrange
            File.WriteAllText(_configurationPath, """
                { "Dlna": { "Server": { "Port": 26852, "AdminPort": 26853 } } }
                """);

            var configuration = new ConfigurationManager();

            // Act
            DlnaConfigurationFile.Add(configuration, _configurationPath, static _ => { }, static _ => { });

            // Assert
            configuration["Dlna:Server:Port"].Should().Be("26852",
                "because an absolute path must resolve - falling back to the code default put the "
                + "server on 26851, which is the reference implementation's port, and the two collided");
            configuration["Dlna:Server:AdminPort"].Should().Be("26853",
                "because the whole file has to load, not just its first value");
        }

        [Test]
        public void Add_WithAnAbsolutePath_BindsOntoTheOptionsTree()
        {
            // Arrange - the shape the server actually reads, including the list that drives the library.
            File.WriteAllText(_configurationPath, """
                {
                  "Dlna": {
                    "Server": { "Port": 26852, "AdminPort": 26853 },
                    "Library": { "SourceFolders": [ "/share/Media" ] },
                    "FileCache": { "MaxTotalSizeInMegabytes": 512 }
                  }
                }
                """);

            var configuration = new ConfigurationManager();
            DlnaConfigurationFile.Add(configuration, _configurationPath, static _ => { }, static _ => { });

            // Act
            var options = configuration.GetSection(DlnaOptions.SectionName).Get<DlnaOptions>()
                ?? new DlnaOptions();

            // Assert
            options.Server.Port.Should().Be(26852, "because the port comes from the file, not the default");
            options.Library.SourceFolders.Should().Contain("/share/Media",
                "because an empty SourceFolders list is what made the server fall back to serving its "
                + "own publish folder");

            // 512, deliberately not the code default. This asserted 256 while claiming the default was
            // 1024 - true when it was written, and the default is 256 now, so the assertion had quietly
            // stopped proving the value came from the file at all.
            options.FileCache.MaxTotalSizeInMegabytes.Should().Be(512,
                "because a cache budget has to come from the file, and asserting the code default would "
                + "pass just as well with the file unread");
        }

        /// <summary>
        /// The half that hid the other one. Ignoring every load failure meant a file that could not be
        /// read at startup produced no error at all - the server simply came up on code defaults.
        /// </summary>
        [Test]
        public void Add_WhenTheFileIsMissingAtStartup_Throws()
        {
            // Arrange - nothing written, so the file does not exist.
            var configuration = new ConfigurationManager();
            var reloadFailures = 0;

            // Act
            var act = () => DlnaConfigurationFile.Add(
                configuration,
                _configurationPath,
                _ => reloadFailures++,
                static _ => { });

            // Assert
            _ = act.Should().Throw<FileNotFoundException>(
                "because a configuration file that cannot be read at startup is not survivable - "
                + "swallowing it started the server on the wrong port against the wrong library");
            reloadFailures.Should().Be(0,
                "because an initial failure is not a reload failure and must not be reported as one");
        }

        [Test]
        public void Add_WhenTheFileIsUnparseableAtStartup_Throws()
        {
            // Arrange
            File.WriteAllText(_configurationPath, "{ this is not json");

            var configuration = new ConfigurationManager();

            // Act
            var act = () => DlnaConfigurationFile.Add(
                configuration,
                _configurationPath,
                static _ => { },
                static _ => { });

            // Assert
            _ = act.Should().Throw<InvalidDataException>(
                "because ConfigurationFileGuard has already replaced an unreadable file by this point, "
                + "so a parse failure here means something is wrong that defaults would only mask");
        }

        /// <summary>
        /// The defect that cost the index. A failed re-read used to leave the provider empty, because
        /// <c>context.Ignore</c> suppresses the rethrow and nothing else.
        /// </summary>
        /// <remarks>
        /// What made it expensive was not the wrong values but what they caused: with the section blank,
        /// <c>DlnaOptionsDefaults</c> falls <c>SourceFolders</c> back to the application folder,
        /// validation passes, and the next scan reconciles every indexed directory out of existence.
        /// </remarks>
        [Test]
        public void Add_WhenAReloadCannotParseTheFile_KeepsTheValuesFromTheLastGoodRead()
        {
            // Arrange
            File.WriteAllText(_configurationPath, """
                { "Dlna": { "Server": { "Port": 26852 }, "Library": { "SourceFolders": [ "/share/Media" ] } } }
                """);

            var configuration = new ConfigurationManager();
            var retained = 0;

            DlnaConfigurationFile.Add(
                configuration,
                _configurationPath,
                static _ => { },
                _ => retained++);

            // Act - the documented NAS workflow, editing config.json over SSH and getting it wrong.
            File.WriteAllText(_configurationPath, "{ this is not json");
            ((IConfigurationRoot)configuration).Reload();

            // Assert
            configuration["Dlna:Server:Port"].Should().Be("26852",
                "because a failed re-read must leave the running configuration alone - it used to blank "
                + "every Dlna key while the log said the opposite");
            configuration["Dlna:Library:SourceFolders:0"].Should().Be("/share/Media",
                "because losing the source folders is what let the watcher move onto the publish folder "
                + "and the next scan cascade-delete the whole index");
            retained.Should().Be(1,
                "because the operator has to be told the file was not applied, and told once per failure");
        }

        /// <summary>
        /// The silent variant, and the more dangerous one: a missing file on reload raises no exception
        /// at all, so the load-exception handler never runs and nothing is reported.
        /// </summary>
        /// <remarks>
        /// Reachable by an ordinary editor save rather than only by a deletion - vim's default
        /// <c>backupcopy=auto</c> writes a temporary file and renames it over the target, which the
        /// watcher sees as a delete followed by a create.
        /// </remarks>
        [Test]
        public void Add_WhenAReloadFindsTheFileMissing_KeepsTheValuesAndSaysSo()
        {
            // Arrange
            File.WriteAllText(_configurationPath, """
                { "Dlna": { "Server": { "Port": 26852 }, "Library": { "SourceFolders": [ "/share/Media" ] } } }
                """);

            var configuration = new ConfigurationManager();
            var reloadFailures = 0;
            var retained = 0;

            DlnaConfigurationFile.Add(
                configuration,
                _configurationPath,
                _ => reloadFailures++,
                _ => retained++);

            // Act
            File.Delete(_configurationPath);
            ((IConfigurationRoot)configuration).Reload();

            // Assert
            configuration["Dlna:Server:Port"].Should().Be("26852",
                "because a file that has momentarily gone is not an instruction to forget every setting");
            configuration["Dlna:Library:SourceFolders:0"].Should().Be("/share/Media",
                "because this is the variant an ordinary editor save reaches, so it must be as safe as "
                + "the unparseable one");
            // Deliberately NOT asserting that nothing was reported. IConfigurationRoot.Reload() drives
            // Load(reload: false), which treats a missing non-optional file as an error; the file
            // watcher drives Load(reload: true), which treats it as optional and reports nothing at all.
            // The retention below has to hold on both paths, which is why the provider decides from the
            // file's own existence rather than from whether an exception arrived.
            _ = reloadFailures;

            retained.Should().Be(1,
                "because the values were kept, and the one thing the old behaviour never did on this "
                + "path was say anything at all");
        }

        /// <summary>
        /// An operator who empties the file is asking for defaults, so this is not a lost read.
        /// </summary>
        [Test]
        public void Add_WhenAReloadFindsAnEmptyObject_TakesItRatherThanKeepingTheOldValues()
        {
            // Arrange
            File.WriteAllText(_configurationPath, """
                { "Dlna": { "Server": { "Port": 26852 } } }
                """);

            var configuration = new ConfigurationManager();
            var retained = 0;

            DlnaConfigurationFile.Add(
                configuration,
                _configurationPath,
                static _ => { },
                _ => retained++);

            // Act
            File.WriteAllText(_configurationPath, "{}");
            ((IConfigurationRoot)configuration).Reload();

            // Assert
            configuration["Dlna:Server:Port"].Should().BeNull(
                "because {} parses, so it is a deliberate edit rather than a failure - keeping the old "
                + "values here would make the file impossible to clear");
            retained.Should().Be(0,
                "because nothing was retained, and reporting otherwise would train the operator to "
                + "ignore the message");
        }
    }
}
