using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;
using DlnaServer.Host.Configuration;
using Microsoft.Extensions.Configuration;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the fallbacks applied between binding and validation. The one that matters is the source
    /// folder: without it, a deployment that has not been configured yet refuses to boot, which is what
    /// the first NAS run hit.
    /// </summary>
    [TestFixture]
    internal sealed class DlnaOptionsDefaultsTest
    {
        [Test]
        public void Apply_WithNoSourceFolders_ServesTheApplicationFolder()
        {
            // Arrange
            var options = new DlnaOptions();

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.SourceFolders.Should().Equal([AppContext.BaseDirectory],
                "because a server with nothing configured should start and serve where it is running from");
        }

        /// <summary>
        /// The fallback and the thumbnail cache would otherwise contradict each other: the cache defaults
        /// to a subdirectory of the application folder, and a cache inside a source folder is a validation
        /// failure unless scanning skips it.
        /// </summary>
        [Test]
        public void Apply_WithNoSourceFolders_ExcludesTheThumbnailCacheFolder()
        {
            // Arrange
            var options = new DlnaOptions();
            var cacheFolderName = Path.GetFileName(
                Path.TrimEndingDirectorySeparator(options.Thumbnails.CacheDirectory));

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.ExcludeFolders.Should().Contain(cacheFolderName,
                "because the cache lives inside the folder that just became the library");
        }

        /// <summary>
        /// A validator run over the fallback's own output is the check that the two agree.
        /// </summary>
        [Test]
        public void Apply_WithNoSourceFolders_ProducesOptionsTheValidatorAccepts()
        {
            // Arrange
            var options = new DlnaOptions();
            options.Server.Port = 26_851;
            options.Server.AdminPort = 26_852;

            // Act
            DlnaOptionsDefaults.Apply(options);
            var result = new DlnaOptionsValidator().Validate(name: null, options: options);

            // Assert
            result.Succeeded.Should().BeTrue(
                "because the fallback exists to make an unconfigured deployment start; failures: {0}",
                result.FailureMessage);
        }

        /// <summary>
        /// The preflight accepts what the validator alone refuses, because startup applies the defaults first.
        /// </summary>
        /// <remarks>
        /// The Settings page validated the raw edit, so clearing the source folders was reported as
        /// "at least one source folder must be listed" - a refusal to save a configuration the server
        /// boots from perfectly well, since <see cref="DlnaOptionsDefaults"/> runs before the validator
        /// does. Both halves are asserted here: the preflight passes, and the bare validator still fails,
        /// which is what makes the difference the point rather than an accident.
        /// </remarks>
        [Test]
        public void Validate_WithNoSourceFolders_PassesPreflightWhereTheBareValidatorFails()
        {
            // Arrange
            var candidate = new DlnaOptions();
            candidate.Server.Port = 26_851;
            candidate.Server.AdminPort = 26_852;

            var preflight = new SettingsPreflight(new DlnaOptionsValidator());

            // Act
            var failures = preflight.Validate(candidate);

            // Assert
            failures.Should().BeEmpty(
                "because startup fills the source folder in before validating, so the page must not "
                + "refuse this; failures: {0}", string.Join(" ", failures));

            new DlnaOptionsValidator().Validate(name: null, options: candidate).Failed.Should().BeTrue(
                "because the validator on its own is what refused it - that gap is the whole defect");
        }

        /// <summary>
        /// The preflight leaves the candidate untouched, because the caller goes on to save it.
        /// </summary>
        /// <remarks>
        /// Defaulting the object the page is about to write is the shape that once grew
        /// <c>config.json</c> by two excluded-folder entries on every save.
        /// </remarks>
        [Test]
        public void Validate_DoesNotModifyTheCandidate()
        {
            // Arrange
            var candidate = new DlnaOptions();
            candidate.Server.Port = 26_851;
            candidate.Server.AdminPort = 26_852;
            candidate.Library.SourceFolders = [AppContext.BaseDirectory];
            candidate.Library.ExcludeFolders = ["Private"];

            // Act
            _ = new SettingsPreflight(new DlnaOptionsValidator()).Validate(candidate);

            // Assert
            candidate.Library.ExcludeFolders.Should().Equal(["Private"],
                "because the defaults seed entries into this list, and they were applied to a copy");
            candidate.Library.SourceFolders.Should().Equal([AppContext.BaseDirectory],
                "because nothing the preflight does may reach the object the page will write");
        }

        [Test]
        public void Apply_WithConfiguredSourceFolders_ChangesNothing()
        {
            // Arrange
            var options = new DlnaOptions
            {
                Library = new LibraryOptions
                {
                    SourceFolders = ["/share/Media"],
                },
            };

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.SourceFolders.Should().Equal(["/share/Media"],
                "because a configured library must never be replaced by the application folder");
            options.Library.ExcludeFolders.Should().Equal([".@__thumb", "@Recycle"],
                "because the fallback's own exclusions are only needed when the application folder became "
                + "the library, and configuration named none of its own so the standard pair is seeded");
        }

        /// <summary>
        /// A list of blank entries is the same mistake as leaving the setting out, and reporting
        /// "contains a blank entry" would be a worse answer than filling it in.
        /// </summary>
        [Test]
        public void Apply_WithOnlyBlankSourceFolders_ServesTheApplicationFolder()
        {
            // Arrange
            var options = new DlnaOptions
            {
                Library = new LibraryOptions
                {
                    SourceFolders = ["", "   "],
                },
            };

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.SourceFolders.Should().Equal([AppContext.BaseDirectory],
                "because no usable folder was named");
        }

        /// <summary>
        /// The application's asset folder holds the device icons, which are JPEGs. Without excluding it a
        /// fresh deployment offers the server's own artwork to a television as a photo album - observed
        /// on the first run of this fallback, which indexed 13 icons as media.
        /// </summary>
        [Test]
        public void Apply_WithNoSourceFolders_ExcludesTheApplicationsOwnAssets()
        {
            // Arrange
            var options = new DlnaOptions();

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.ExcludeFolders.Should().Contain("Resources",
                "because the device icons are protocol assets, not somebody's photographs");
        }

        [Test]
        public void Apply_WithANestedCacheDirectory_ExcludesItsOwnFolderName()
        {
            // Arrange
            var options = new DlnaOptions
            {
                Thumbnails = new ThumbnailOptions
                {
                    CacheDirectory = Path.Combine(AppContext.BaseDirectory, "cache", "previews"),
                },
            };

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.ExcludeFolders.Should().Contain("previews",
                "because the exclusion has to match the folder the thumbnails actually land in");
        }

        [Test]
        public void Apply_WhenTheCacheFolderIsAlreadyExcluded_DoesNotDuplicateIt()
        {
            // Arrange
            var options = new DlnaOptions
            {
                Library = new LibraryOptions
                {
                    ExcludeFolders = ["thumbnails"],
                },
                Thumbnails = new ThumbnailOptions
                {
                    CacheDirectory = Path.Combine(AppContext.BaseDirectory, "thumbnails"),
                },
            };

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.ExcludeFolders.Should().Equal(["thumbnails", ".@__thumb", "Resources"],
                "because configuration reloads re-run this, and each one would otherwise append again - "
                + "the thumbnail sub-folder is added unconditionally and belongs in the expected list");
        }

        /// <summary>
        /// The duplication bug this method exists to end. <c>ConfigurationBinder</c> adds to a non-empty
        /// list rather than replacing it, so a default declared on the property appended itself to
        /// whatever the file named - and the admin UI wrote the longer list back, so the file grew by two
        /// entries on every save. Observed live as
        /// <c>[".@__thumb", "@Recycle", ".@__thumb", "@Recycle", "Personal", ...]</c>.
        /// </summary>
        [Test]
        public void Apply_WithRepeatedExclusions_KeepsOneOfEachInOrder()
        {
            // Arrange
            var options = new DlnaOptions
            {
                Library = new LibraryOptions
                {
                    SourceFolders = ["/share/Media"],
                    ExcludeFolders = [".@__thumb", "@Recycle", ".@__THUMB", "@recycle", "Personal"],
                },
            };

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.ExcludeFolders.Should().Equal([".@__thumb", "@Recycle", "Personal"],
                "because repeats match case-insensitively and the operator's own order is what the "
                + "settings page shows back to them");
        }

        /// <summary>
        /// A reload runs this again over its own output, and so does a save made through the admin UI, so
        /// the second pass has to be a no-op or the file grows without end.
        /// </summary>
        [Test]
        public void Apply_AppliedTwice_ChangesNothingTheSecondTime()
        {
            // Arrange
            var options = new DlnaOptions
            {
                Library = new LibraryOptions
                {
                    SourceFolders = ["/share/Media"],
                    ExcludeFolders = [".@__thumb", "@Recycle", "Personal"],
                },
            };

            // Act
            DlnaOptionsDefaults.Apply(options);
            var afterFirst = options.Library.ExcludeFolders.ToArray();
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.ExcludeFolders.Should().Equal(afterFirst,
                "because binding, PostConfigure and a save through the admin UI form a loop, and a pass "
                + "that adds anything turns that loop into unbounded growth");
        }

        /// <summary>
        /// Blank entries are dropped rather than kept: they match nothing in either half of the setting,
        /// and one shown on the settings page reads as a bug in the page.
        /// </summary>
        [Test]
        public void Apply_WithBlankExclusions_DropsThem()
        {
            // Arrange
            var options = new DlnaOptions
            {
                Library = new LibraryOptions
                {
                    SourceFolders = ["/share/Media"],
                    ExcludeFolders = ["@Recycle", "", "   "],
                },
            };

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.ExcludeFolders.Should().Equal(["@Recycle", ".@__thumb"],
                "because a blank name excludes nothing, and the thumbnail sub-folder is always added");
        }

        /// <summary>
        /// Seeding only fills an empty list. An operator who named their own exclusions gets exactly
        /// those - plus the thumbnail sub-folder, which is not optional - so <c>@Recycle</c> can be
        /// dropped by someone who does not want it.
        /// </summary>
        [Test]
        public void Apply_WithConfiguredExclusions_DoesNotSeedTheStandardPair()
        {
            // Arrange
            var options = new DlnaOptions
            {
                Library = new LibraryOptions
                {
                    SourceFolders = ["/share/Media"],
                    ExcludeFolders = ["Personal"],
                },
            };

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.ExcludeFolders.Should().Equal(["Personal", ".@__thumb"],
                "because a named list is the operator's, and only the thumbnail sub-folder is forced");
        }

        [Test]
        public void Apply_WithNoSubtitleTypes_SeedsTheShippedOnes()
        {
            // Arrange
            var options = new DlnaOptions { Library = new LibraryOptions { SourceFolders = ["/share/Media"] } };

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.SubtitleFileExtensions.Should().BeEquivalentTo(SubtitleFileExtensionDefaults.Create(),
                "because a configuration that names no subtitle types still links the usual ones");
        }

        /// <summary>
        /// The append trap <c>ExcludeFolders</c> fell into: a default on the property would be merged into
        /// whatever <c>config.json</c> named, so a type the operator removed would come straight back.
        /// </summary>
        [Test]
        public void Apply_AfterBindingAConfigurationThatNamesItsOwnSubtitleTypes_KeepsOnlyThose()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Dlna:Library:SourceFolders:0"] = "/share/Media",
                    ["Dlna:Library:SubtitleFileExtensions:.txt"] = "Video",
                    ["Dlna:Library:SubtitleFileExtensions:.lrc"] = "Audio",
                })
                .Build();
            var options = new DlnaOptions();
            configuration.GetSection(DlnaOptions.SectionName).Bind(options);

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.SubtitleFileExtensions.Should().BeEquivalentTo(
                new Dictionary<string, DlnaMedia> { [".txt"] = DlnaMedia.Video, [".lrc"] = DlnaMedia.Audio },
                "because a named list is the operator's, and none of the shipped types may be appended to it");
        }

        [Test]
        public void Apply_WithSubtitleTypesInAnyForm_StoresThemLowerCaseWithALeadingDotAndLooksThemUpInAnyCase()
        {
            // Arrange
            var options = new DlnaOptions
            {
                Library = new LibraryOptions
                {
                    SourceFolders = ["/share/Media"],
                    SubtitleFileExtensions = new Dictionary<string, DlnaMedia>(StringComparer.Ordinal)
                    {
                        [" SRT "] = DlnaMedia.Video,
                        [".Lrc"] = DlnaMedia.Audio,
                    },
                },
            };

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.SubtitleFileExtensions.Keys.Should().BeEquivalentTo([".srt", ".lrc"],
                "because the stored form matches the file types editor, which normalises the same way");
            options.Library.SubtitleFileExtensions.ContainsKey(".SRT").Should().BeTrue(
                "because a file's own extension is looked up in whatever case it has on disc");
        }

        [Test]
        public void Apply_AppliedTwice_LeavesTheSubtitleTypesAsTheFirstPassLeftThem()
        {
            // Arrange
            var options = new DlnaOptions { Library = new LibraryOptions { SourceFolders = ["/share/Media"] } };
            DlnaOptionsDefaults.Apply(options);
            var afterFirst = new Dictionary<string, DlnaMedia>(options.Library.SubtitleFileExtensions);

            // Act
            DlnaOptionsDefaults.Apply(options);

            // Assert
            options.Library.SubtitleFileExtensions.Should().BeEquivalentTo(afterFirst,
                "because this runs again on every configuration reload and must not grow or change the list");
        }
    }
}
