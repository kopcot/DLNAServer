using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;
using DlnaServer.Host.Configuration;
using Microsoft.Extensions.Options;

namespace DlnaServer.IntegrationTests
{
    [TestFixture]
    internal sealed class DlnaOptionsValidatorTest
    {
        private string _sourceFolder = string.Empty;
        private string _thumbnailFolder = string.Empty;
        private DlnaOptionsValidator _validator = null!;

        [SetUp]
        public void SetUp()
        {
            _sourceFolder = Directory.CreateTempSubdirectory("dlna-src-").FullName;
            _thumbnailFolder = Directory.CreateTempSubdirectory("dlna-thumbs-").FullName;
            _validator = new DlnaOptionsValidator();
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_sourceFolder, recursive: true);
            Directory.Delete(_thumbnailFolder, recursive: true);
        }

        [Test]
        public void Validate_WithValidOptions_Succeeds()
        {
            // Arrange
            var options = CreateValidOptions();

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Succeeded.Should().BeTrue(
                "because a fully populated configuration pointing at existing folders is valid; failures: {0}",
                result.FailureMessage);
        }

        [Test]
        public void Validate_WhenMediaAndAdminPortsAreEqual_Fails()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Server.AdminPort = options.Server.Port;

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because sharing one port would expose the admin UI to every renderer on the network");
            result.FailureMessage.Should().Contain("admin port",
                "because the message must name the setting that has to change, as the Settings page labels it");
            result.FailureMessage.Should().NotContain(nameof(ServerOptions.AdminPort),
                "because the Settings page renders this verbatim to an operator, who does not read code");
        }

        [Test]
        public void Validate_WithNoSourceFolders_Fails()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Library.SourceFolders.Clear();

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because the validator sees the bound options as they are - a real deployment never reaches "
                + "this state, since DlnaOptionsDefaults fills in the application folder first");
        }

        /// <summary>
        /// The arrangement the source-folder fallback produces: the application folder is the library and
        /// the thumbnail cache sits inside it, excluded by name so scanning skips it.
        /// </summary>
        [Test]
        public void Validate_WhenThumbnailCacheIsInsideSourceFolderButExcluded_Succeeds()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Thumbnails.CacheDirectory = Path.Combine(_sourceFolder, "thumbnails");
            options.Library.ExcludeFolders.Add("thumbnails");

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Succeeded.Should().BeTrue(
                "because an excluded folder is never scanned, so its thumbnails cannot be re-indexed; failures: {0}",
                result.FailureMessage);
        }

        [Test]
        public void Validate_WhenAnIntermediateFolderIsExcluded_Succeeds()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Thumbnails.CacheDirectory = Path.Combine(_sourceFolder, "cache", "thumbs");
            options.Library.ExcludeFolders.Add("cache");

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Succeeded.Should().BeTrue(
                "because exclusion is matched per path segment, so excluding a parent prunes everything "
                + "beneath it; failures: {0}",
                result.FailureMessage);
        }

        /// <summary>
        /// No exclusion can save this one: excluding the source folder itself would mean scanning nothing.
        /// </summary>
        [Test]
        public void Validate_WhenThumbnailCacheIsTheSourceFolderItself_Fails()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Thumbnails.CacheDirectory = _sourceFolder;

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because thumbnails would be written directly into the folder being scanned");
        }

        /// <summary>
        /// Deliberately the opposite of what this asserted before. Existence was checked here, which put
        /// filesystem availability inside validation - and validation re-runs on every configuration
        /// reload, throwing out of <c>IOptionsMonitor.CurrentValue</c>, which is read on every media
        /// request, every listing and every database command. So an unmounted share plus any touch of
        /// config.json turned a transient mount problem into a total outage, including the Settings page
        /// that would have fixed it. Availability is reported by <c>ISourceFolderChecker</c> and stops
        /// reconciliation through <c>LibraryIndexer.FindUnusableSourceFolders</c>; neither can take the
        /// server down.
        /// </summary>
        [Test]
        public void Validate_WithMissingSourceFolder_Succeeds()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Library.SourceFolders.Add(Path.Combine(_sourceFolder, "does-not-exist"));

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeFalse(
                "because a folder that is merely absent may be a share still spinning up, and refusing "
                + "to boot over it is what made a reload of a valid file able to 500 the whole server");
        }

        [Test]
        public void Validate_WithARelativeSourceFolder_Fails()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Library.SourceFolders.Add("media/films");

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because where a relative folder points depends on how the server was started, which is "
                + "a mistake in the file rather than a condition on the disc");
        }

        [Test]
        [TestCase("..")]
        [TestCase(".")]
        [TestCase("/absolute/thumbs")]
        [TestCase("nested/thumbs")]
        public void Validate_WithAThumbnailSubFolderThatIsAPath_Fails(string subFolderName)
        {
            // Arrange
            var options = CreateValidOptions();
            options.Thumbnails.SubFolderName = subFolderName;

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because Path.Combine discards the media folder for a rooted value - collapsing every "
                + "folder's thumbnails onto one path that carries a unique index - and exclusion matches "
                + "one whole segment, so a value with a separator stops the scanner skipping it and every "
                + "preview is re-ingested as media");
        }

        [Test]
        public void Validate_WithATooShortExcludeFolder_Fails()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Library.ExcludeFolders.Add("e");

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because hiding matches any path containing the entry, so one character typed into the "
                + "Settings page hid the whole library from renderers and from the admin UI while the "
                + "scanner carried on indexing it");
        }

        /// <summary>
        /// A partial path is now a legal exclusion entry.
        /// </summary>
        /// <remarks>
        /// This test asserted the opposite. Refusing a separator was half of a customer-reported defect:
        /// an operator could not name <c>Films/Private</c> to hide one branch, and the entry they could
        /// give - <c>Private</c> - was matched as a raw substring, so it hid every folder whose name
        /// merely contained it. Both halves were fixed together; see
        /// <c>PathExclusionTest.IsHidden_WithAPartialPath_MatchesTheWholeRunOfSegments</c>.
        /// </remarks>
        [TestCase("Films/Private")]
        [TestCase("Films\\Private")]
        [TestCase("/Films/Private/")]
        public void Validate_WithAPathShapedExcludeFolder_Succeeds(string entry)
        {
            // Arrange
            var options = CreateValidOptions();
            options.Library.ExcludeFolders.Add(entry);

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Succeeded.Should().BeTrue(
                $"because '{entry}' names a run of folders, which is what lets one branch be hidden "
                + "without hiding every folder that shares its last name");
        }

        /// <summary>
        /// Pasting a folder's full path is the obvious thing to try, and it works.
        /// </summary>
        /// <remarks>
        /// An intermediate version of this change refused a rooted entry, on the theory that a segment
        /// run could not match one. That was wrong: <c>PathExclusion</c> trims the surrounding
        /// separators and matches a run of whole segments anywhere in the path, so every segment of an
        /// absolute path lines up with the stored one. Refusing it would have rejected exactly the input
        /// the customer's request describes - "a path from the current server".
        /// </remarks>
        [Test]
        public void Validate_WithAFullPathExcludeFolder_Succeeds()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Library.ExcludeFolders.Add(Path.Combine(_sourceFolder, "Private"));

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Succeeded.Should().BeTrue(
                "because a full path is a run of folder names like any other; failures: {0}",
                result.FailureMessage);
        }

        [TestCase("Films/./Private")]
        [TestCase("Films/../Private")]
        public void Validate_WithARelativeSegmentInAnExcludeFolder_Fails(string entry)
        {
            // Arrange
            var options = CreateValidOptions();
            options.Library.ExcludeFolders.Add(entry);

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                $"because nothing resolves '{entry}' against a folder, so the relative segment can never "
                + "match a stored path");
        }

        [Test]
        public void Validate_WhenThumbnailCacheIsInsideSourceFolder_Fails()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Thumbnails.CacheDirectory = Path.Combine(_sourceFolder, "thumbs");

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because thumbnails written inside a source folder get re-indexed as media on the next scan");
        }

        [Test]
        public void Validate_WhenMaxFileSizeExceedsTotalCacheSize_Fails()
        {
            // Arrange
            var options = CreateValidOptions();
            options.FileCache.MaxTotalSizeInMegabytes = 100;
            options.FileCache.MaxFileSizeInMegabytes = 200;

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because a per-file limit above the total budget means such a file could never be cached");
        }

        [Test]
        public void Validate_WithAnUploadFolderInsideASourceFolder_Succeeds()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Upload.DestinationFolder = Path.Combine(_sourceFolder, "Incoming");

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Succeeded.Should().BeTrue(
                "because a folder under a source folder is exactly where an upload belongs, and the "
                + "folder not existing yet is not validation's question; failures: {0}",
                result.FailureMessage);
        }

        [Test]
        public void Validate_WithAnUploadFolderOutsideEverySourceFolder_Fails()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Upload.DestinationFolder = _thumbnailFolder;

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because a destination the library never scans would accept files and then never show "
                + "them, which reads to the operator as the upload having silently failed");
        }

        [TestCase("Private")]
        [TestCase("Films/Private/Incoming")]
        [TestCase(".@__thumb")]
        public void Validate_WithAnUploadFolderTheLibrarySkips_Fails(string suffix)
        {
            // Arrange
            var options = CreateValidOptions();
            options.Library.ExcludeFolders.Add("Private");
            options.Upload.DestinationFolder = Path.Combine(_sourceFolder, suffix);

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because every upload into a skipped folder is refused at request time, so the Settings page "
                + "must not accept it as the destination");
            result.FailureMessage.Should().Contain("upload folder",
                "because the message must name the setting that has to change, as the Settings page labels it");
        }

        [TestCase("..")]
        [TestCase("Incoming/../../elsewhere")]
        public void Validate_WithARelativeSegmentInTheUploadFolder_Fails(string suffix)
        {
            // Arrange
            var options = CreateValidOptions();
            options.Upload.DestinationFolder = Path.Combine(_sourceFolder, suffix);

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because '..' is how a path climbs out of the folder it was given, and a configured "
                + "destination is not the place to discover that at upload time");
        }

        [Test]
        public void Validate_WithAnUploadSizeBelowTheRange_Fails()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Upload.MaxSizeInMegabytes = 0;

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because a limit of zero would refuse every file while looking like a configured value - "
                + "the annotation on the new group has to be validated like every other one");
        }

        [Test]
        public void Validate_WithTheShippedSubtitleTypes_Succeeds()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Library.SubtitleFileExtensions = SubtitleFileExtensionDefaults.Create();
            options.Library.MediaFileExtensions = MediaFileExtensionDefaults.Create();

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Succeeded.Should().BeTrue(
                "because the shipped subtitle types and the shipped file types never overlap; failures: {0}",
                result.FailureMessage);
        }

        [TestCase(".")]
        [TestCase(".s rt")]
        [TestCase(".sub/x")]
        public void Validate_ForASubtitleTypeThatIsNotAnExtension_Fails(string extension)
        {
            // Arrange
            var options = CreateValidOptions();
            options.Library.SubtitleFileExtensions = new Dictionary<string, DlnaMedia> { [extension] = DlnaMedia.Video };

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue($"because '{extension}' can never be the ending of a file name");
            result.FailureMessage.Should().Contain("subtitle types",
                "because the message names the setting as the Settings page labels it");
        }

        [TestCase(DlnaMedia.Image)]
        [TestCase(DlnaMedia.Unknown)]
        public void Validate_ForASubtitleTypeGoingWithNeitherVideoNorMusic_Fails(DlnaMedia kind)
        {
            // Arrange
            var options = CreateValidOptions();
            options.Library.SubtitleFileExtensions = new Dictionary<string, DlnaMedia> { [".srt"] = kind };

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue($"because a subtitle linked to {kind} would never match anything");
        }

        [TestCase(".mkv")]
        [TestCase(".flac")]
        public void Validate_ForASubtitleTypeThatIsAlsoMedia_Fails(string extension)
        {
            // Arrange
            var options = CreateValidOptions();
            options.Library.MediaFileExtensions = MediaFileExtensionDefaults.Create();
            options.Library.SubtitleFileExtensions = new Dictionary<string, DlnaMedia> { [extension] = DlnaMedia.Video };

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                $"because the scanner resolves '{extension}' as media first - from the file types or its own catalog - "
                + "so those files would be indexed on their own and never linked");
            result.FailureMessage.Should().Contain(extension, "because the operator has to know which line to change");
        }

        private DlnaOptions CreateValidOptions()
        {
            return new DlnaOptions
            {
                Server = new ServerOptions
                {
                    Port = 26_851,
                    AdminPort = 26_852,
                },
                Library = new LibraryOptions
                {
                    SourceFolders = [_sourceFolder],
                },
                Thumbnails = new ThumbnailOptions
                {
                    CacheDirectory = _thumbnailFolder,
                },
            };
        }
    }
}
