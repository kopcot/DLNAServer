using DlnaServer.Admin.Configuration;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;
using DlnaServer.Upnp.Constants;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// The file-type map is the only thing deciding whether a file on disc counts as media, so these
    /// cover the ways an edit to it can quietly cost a whole file type - or the whole library.
    /// </summary>
    [TestFixture]
    internal sealed class ExtensionMapTest
    {
        /// <summary>
        /// A dictionary hands its entries back in insertion order and every save rewrites it, so without
        /// an explicit order the list would rearrange itself between visits.
        /// </summary>
        [Test]
        public void FromOptions_OrdersRowsByExtension()
        {
            // Arrange
            var configured = new Dictionary<string, MediaExtensionOptions>(StringComparer.OrdinalIgnoreCase)
            {
                [".mkv"] = new() { Mime = nameof(DlnaMime.VideoXMatroska) },
                [".avi"] = new() { Mime = nameof(DlnaMime.VideoAvi) },
                [".jpg"] = new() { Mime = nameof(DlnaMime.ImageJpeg) },
            };

            // Act
            var rows = ExtensionMap.FromOptions(configured);

            // Assert
            rows.Select(static r => r.Extension).Should().ContainInOrder(
                [".avi", ".jpg", ".mkv"],
                "because the editor must show the same order every time it is opened");
        }

        /// <summary>
        /// The defect this defends against: the indexer silently skips an extension whose stored type
        /// name it cannot parse, so the file type is never indexed and nothing reports it.
        /// </summary>
        [Test]
        public void FromOptions_ForAnUnreadableTypeName_LeavesTheRowWithoutAType()
        {
            // Arrange
            var configured = new Dictionary<string, MediaExtensionOptions>(StringComparer.OrdinalIgnoreCase)
            {
                [".mkv"] = new() { Mime = "NotARealMimeName" },
            };

            // Act
            var rows = ExtensionMap.FromOptions(configured);

            // Assert
            rows.Should().ContainSingle("because the entry is still there to be corrected")
                .Which.Mime.Should().Be(DlnaMime.Undefined,
                    "because an unreadable name must show as unset, so validation refuses it rather than "
                        + "the editor hiding a broken entry behind a plausible default");
        }

        /// <summary>
        /// Saving no file types at all would leave the server treating nothing as media.
        /// </summary>
        [Test]
        public void Validate_ForNoRows_RefusesTheSave()
        {
            // Arrange
            List<ExtensionMapRow> rows = [];

            // Act
            var problems = ExtensionMap.Validate(rows);

            // Assert
            problems.Should().ContainSingle(
                "because an empty map is the one edit on that page which empties the whole library");
        }

        /// <summary>
        /// Two lines naming the same extension differently still collide, because the map is keyed
        /// without regard to case and an extension is stored with its leading dot.
        /// </summary>
        [Test]
        public void Validate_ForTheSameExtensionWrittenTwoWays_ReportsTheDuplicate()
        {
            // Arrange
            List<ExtensionMapRow> rows =
            [
                new() { Extension = ".mkv", Mime = DlnaMime.VideoXMatroska, ProfileName = "MATROSKA" },
                new() { Extension = "MKV", Mime = DlnaMime.VideoAvi, ProfileName = "AVI" },
            ];

            // Act
            var problems = ExtensionMap.Validate(rows);

            // Assert
            problems.Should().ContainSingle("because the two lines resolve to one key")
                .Which.Should().Contain(".mkv",
                    "because the message has to name the extension the operator must go and fix");
        }

        /// <summary>
        /// A row with no type chosen would be skipped by the indexer, so it is refused instead.
        /// </summary>
        [Test]
        public void Validate_ForARowWithoutAType_ReportsIt()
        {
            // Arrange
            List<ExtensionMapRow> rows =
                [new() { Extension = ".mkv", Mime = DlnaMime.Undefined, ProfileName = "MATROSKA" }];

            // Act
            var problems = ExtensionMap.Validate(rows);

            // Assert
            problems.Should().ContainSingle("because saving it would silently ignore every .mkv file")
                .Which.Should().Contain(".mkv", "because the operator has to know which line is wrong");
        }

        [Test]
        public void Validate_ForAnExtensionContainingASlash_ReportsIt()
        {
            // Arrange
            List<ExtensionMapRow> rows = [new() { Extension = ".mk/v", Mime = DlnaMime.VideoXMatroska }];

            // Act
            var problems = ExtensionMap.Validate(rows);

            // Assert
            problems.Should().ContainSingle("because an extension holding a path separator can match nothing");
        }

        [Test]
        public void Validate_ForARowWithNoExtensionAtAll_ReportsIt()
        {
            // Arrange
            List<ExtensionMapRow> rows = [new() { Extension = "   ", Mime = DlnaMime.VideoXMatroska }];

            // Act
            var problems = ExtensionMap.Validate(rows);

            // Assert
            problems.Should().ContainSingle("because a blank line cannot be saved as a file type");
        }

        [Test]
        public void Validate_ForUsableRows_ReportsNothing()
        {
            // Arrange
            List<ExtensionMapRow> rows =
            [
                new() { Extension = ".mkv", Mime = DlnaMime.VideoXMatroska, ProfileName = "MATROSKA" },
                new() { Extension = "jpg", Mime = DlnaMime.ImageJpeg, ProfileName = "JPEG_LRG" },
            ];

            // Act
            var problems = ExtensionMap.Validate(rows);

            // Assert
            problems.Should().BeEmpty(
                "because a missing leading dot is normalised rather than refused - it is not a mistake "
                    + "worth stopping a save for");
        }

        /// <summary>
        /// A blank profile is shown filled in, because blank never meant "none".
        /// </summary>
        /// <remarks>
        /// The server resolves a blank profile to the type's default and sends that, so an empty box hid
        /// the value televisions were actually receiving. The whole live library was configured this way.
        /// </remarks>
        [Test]
        public void FromOptions_ForABlankProfile_FillsInTheDefaultTheServerWouldSend()
        {
            // Arrange - "" is exactly what the deployed config.json holds for every extension.
            var configured = new Dictionary<string, MediaExtensionOptions>(StringComparer.OrdinalIgnoreCase)
            {
                [".mkv"] = new() { Mime = nameof(DlnaMime.VideoXMatroska), ProfileName = "" },
            };

            // Act
            var rows = ExtensionMap.FromOptions(configured);

            // Assert
            rows.Should().ContainSingle().Which.ProfileName.Should().Be("MATROSKA",
                "because that is the profile the type resolves to, and showing it blank hid it");
        }

        /// <summary>
        /// A type the catalog has no profiles for falls back to the extension, as the wire layer does.
        /// </summary>
        [Test]
        public void FromOptions_ForATypeWithNoKnownProfiles_FallsBackToTheExtension()
        {
            // Arrange - VideoOgg carries no profile names.
            var configured = new Dictionary<string, MediaExtensionOptions>(StringComparer.OrdinalIgnoreCase)
            {
                [".ogv"] = new() { Mime = nameof(DlnaMime.VideoOgg), ProfileName = null },
            };

            // Act
            var rows = ExtensionMap.FromOptions(configured);

            // Assert
            rows.Should().ContainSingle().Which.ProfileName.Should().Be("OGV",
                "because the wire layer falls back to the upper-cased extension when the type has none");
        }

        /// <summary>
        /// The pre-filled default has to be the value that actually reaches a television.
        /// </summary>
        /// <remarks>
        /// <c>ExtensionMap</c> restates a rule that lives in <c>DlnaProtocolInfo.ResolveProfile</c>, which
        /// is private and in an assembly the admin project cannot reference. This is the test that keeps
        /// the two honest - the class of defect M1 removed elsewhere by deleting the second copy.
        /// </remarks>
        [Test]
        public void DefaultProfileFor_IsTheProfileThatReachesTheWire()
        {
            // Arrange
            var cases = new[]
            {
                (Mime: DlnaMime.VideoXMatroska, Extension: ".mkv"),
                (Mime: DlnaMime.VideoMp4, Extension: ".mp4"),
                (Mime: DlnaMime.ImageJpeg, Extension: ".jpg"),
                (Mime: DlnaMime.VideoOgg, Extension: ".ogv"),
            };

            foreach (var (mime, extension) in cases)
            {
                // Act
                var prefilled = ExtensionMap.DefaultProfileFor(mime, extension);
                var onTheWire = DlnaProtocolInfo.ContentFeaturesFor(mime, dlnaProfileName: null, extension);

                // Assert
                onTheWire.Should().Contain($"DLNA.ORG_PN={prefilled};",
                    $"because what the editor pre-fills for {mime} must be what a television is told");
            }
        }

        [Test]
        public void Validate_ForARowWithNoProfile_ReportsIt()
        {
            // Arrange
            List<ExtensionMapRow> rows =
                [new() { Extension = ".mkv", Mime = DlnaMime.VideoXMatroska, ProfileName = "  " }];

            // Act
            var problems = ExtensionMap.Validate(rows);

            // Assert
            problems.Should().ContainSingle("because every line must carry a profile now that one is offered")
                .Which.Should().Contain(".mkv", "because the message has to name the line to fix");
        }

        /// <summary>
        /// What is written back is the shape the configuration stores and the indexer reads.
        /// </summary>
        [Test]
        public void ToOptions_StoresTheExtensionLowerCasedWithItsLeadingDot()
        {
            // Arrange
            List<ExtensionMapRow> rows = [new() { Extension = "  MKV ", Mime = DlnaMime.VideoXMatroska }];

            // Act
            var map = ExtensionMap.ToOptions(rows);

            // Assert
            map.Should().ContainKey(".mkv",
                "because that is the form the configuration documents and the indexer looks up");

            map[".mkv"].Mime.Should().Be(nameof(DlnaMime.VideoXMatroska),
                "because the map stores the type as the name of an enum member, not as its number");
        }

        /// <summary>
        /// A blank profile has to become null, not an empty string.
        /// </summary>
        /// <remarks>
        /// The indexer reads <c>ProfileName ?? mime.ToMainProfileName()</c>, and an empty string is not
        /// null - so a blank stored as "" is advertised to a television verbatim and the type's own
        /// default profile is never reached.
        /// </remarks>
        [Test]
        public void ToOptions_ForABlankProfile_StoresNullSoTheTypeDefaultApplies()
        {
            // Arrange
            List<ExtensionMapRow> rows = [new() { Extension = ".mkv", Mime = DlnaMime.VideoXMatroska, ProfileName = "  " }];

            // Act
            var map = ExtensionMap.ToOptions(rows);

            // Assert
            map[".mkv"].ProfileName.Should().BeNull(
                "because only null makes the indexer fall back to the type's own default profile");
        }

        [Test]
        public void ToOptions_KeysTheMapWithoutRegardToCase()
        {
            // Arrange
            List<ExtensionMapRow> rows = [new() { Extension = ".mkv", Mime = DlnaMime.VideoXMatroska }];

            // Act
            var map = ExtensionMap.ToOptions(rows);

            // Assert
            map.ContainsKey(".MKV").Should().BeTrue(
                "because a file on disc may be named .MKV and the indexer looks the extension up in this map");
        }
    }
}
