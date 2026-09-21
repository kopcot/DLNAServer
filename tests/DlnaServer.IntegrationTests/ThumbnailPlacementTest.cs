using DlnaServer.Core.Configuration;
using DlnaServer.Host.Configuration;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers where thumbnails are written and the exclusion that makes it safe. Thumbnails live beside
    /// the media, as the reference stores them, so a library that has already been previewed survives a
    /// redeploy instead of being regenerated in full.
    /// </summary>
    [TestFixture]
    internal sealed class ThumbnailPlacementTest
    {
        private string _sourceFolder = string.Empty;
        private DlnaOptionsValidator _validator = null!;

        [SetUp]
        public void SetUp()
        {
            _sourceFolder = Directory.CreateTempSubdirectory("dlna-thumb-place-").FullName;
            _validator = new DlnaOptionsValidator();
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_sourceFolder, recursive: true);
        }

        [Test]
        public void SubFolderName_DefaultsToTheReferenceFolder()
        {
            // Assert
            new ThumbnailOptions().SubFolderName.Should().Be(".@__thumb",
                "because an existing library already has its previews in that folder, and the point of "
                + "matching the reference is to adopt them rather than regenerate them");
        }

        /// <summary>
        /// The exclusion is no longer something the two settings have to be kept in step by hand: the
        /// sub-folder is added to the list after binding, whatever the list says.
        /// </summary>
        [Test]
        public void Apply_AlwaysExcludesTheThumbnailSubFolder()
        {
            // Arrange
            var options = new DlnaOptions
            {
                Library = new LibraryOptions
                {
                    SourceFolders = [_sourceFolder],
                    ExcludeFolders = ["@Recycle"],
                },
                Thumbnails = new ThumbnailOptions { SubFolderName = ".previews" },
            };

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.ExcludeFolders.Should().Contain(".previews",
                "because thumbnails are written into that folder beside every media file, so a scan that "
                + "does not skip it re-ingests every preview as media and then previews those");
        }

        [Test]
        public void Validate_WhenTheThumbnailSubFolderIsExcluded_Succeeds()
        {
            // Arrange
            var options = CreateOptions();
            options.Thumbnails.SubFolderName = ".previews";
            options.Library.ExcludeFolders.Add(".previews");

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Succeeded.Should().BeTrue(
                "because an excluded folder is never scanned; failures: {0}", result.FailureMessage);
        }

        /// <summary>
        /// Thumbnails are always written beside the media, so there is no configuration in which the
        /// sub-folder has no name - blank used to select the central cache and now fails validation.
        /// </summary>
        [Test]
        public void Validate_WithNoSubFolderName_Fails()
        {
            // Arrange
            var options = CreateOptions();
            options.Thumbnails.SubFolderName = string.Empty;

            // Act
            var result = _validator.Validate(name: null, options: options);

            // Assert
            result.Failed.Should().BeTrue(
                "because a blank name leaves nothing for the scanner to skip and nothing to write into");
            result.FailureMessage.Should().Contain("preview folder name",
                "because the message must name the setting that is missing, as the Settings page labels it");
            result.FailureMessage.Should().NotContain(nameof(ThumbnailOptions.SubFolderName),
                "because the Settings page renders this verbatim to an operator, who does not read code");
        }

        private DlnaOptions CreateOptions()
        {
            return new DlnaOptions
            {
                Server = new ServerOptions { Port = 26_851, AdminPort = 26_852 },
                Library = new LibraryOptions { SourceFolders = [_sourceFolder] },
            };
        }
    }
}
