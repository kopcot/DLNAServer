using DlnaServer.Admin.Configuration;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the row half of the Settings page's subtitle types - what the options validator cannot see,
    /// because the map the rows become can hold neither a blank nor a repeated extension.
    /// </summary>
    [TestFixture]
    internal sealed class SubtitleTypeMapTest
    {
        [Test]
        public void Defaults_AreTheShippedTypesInOrder()
        {
            // Act
            var rows = SubtitleTypeMap.Defaults();

            // Assert
            rows.Select(static r => (r.Extension, r.Kind)).Should().Equal(
                [
                    (".ass", DlnaMedia.Video),
                    (".lrc", DlnaMedia.Audio),
                    (".smi", DlnaMedia.Video),
                    (".srt", DlnaMedia.Video),
                    (".ssa", DlnaMedia.Video),
                    (".sub", DlnaMedia.Video),
                    (".vtt", DlnaMedia.Video),
                ],
                "because Restore the default subtitle types has to bring back exactly what the server ships with, "
                + "ordered the way the editor always shows it");
        }

        [Test]
        public void Validate_ForTheDefaults_FindsNothing()
        {
            // Act
            var problems = SubtitleTypeMap.Validate(rows: SubtitleTypeMap.Defaults());

            // Assert
            problems.Should().BeEmpty("because the shipped list has to be saveable as it is");
        }

        [Test]
        public void Validate_ForNoRows_RefusesAndNamesTheSwitchThatDoesStopSubtitles()
        {
            // Act
            var problems = SubtitleTypeMap.Validate(rows: []);

            // Assert
            problems.Should().ContainSingle("because an empty list is one mistake")
                .Which.Should().Contain("Offer subtitles to televisions",
                    "because an empty list only brings the defaults back, so the operator wanting none needs the real switch");
        }

        [Test]
        public void Validate_ForABlankExtension_Refuses()
        {
            // Arrange
            List<SubtitleTypeRow> rows = [new() { Extension = "  ", Kind = DlnaMedia.Video }];

            // Act
            var problems = SubtitleTypeMap.Validate(rows: rows);

            // Assert
            problems.Should().ContainSingle("because a line with no extension cannot become an entry in the map");
        }

        [Test]
        public void Validate_ForTheSameExtensionTwiceInAnyForm_Refuses()
        {
            // Arrange
            List<SubtitleTypeRow> rows =
            [
                new() { Extension = ".srt", Kind = DlnaMedia.Video },
                new() { Extension = "SRT", Kind = DlnaMedia.Audio },
            ];

            // Act
            var problems = SubtitleTypeMap.Validate(rows: rows);

            // Assert
            problems.Should().ContainSingle("because .srt and SRT are the same extension once normalised")
                .Which.Should().Contain(".srt", "because the message names the repeated extension in its stored form");
        }

        [Test]
        public void ToOptions_ThenFromOptions_RoundTripsNormalisedRows()
        {
            // Arrange
            List<SubtitleTypeRow> rows =
            [
                new() { Extension = " TXT ", Kind = DlnaMedia.Video },
                new() { Extension = ".lrc", Kind = DlnaMedia.Audio },
            ];

            // Act
            var map = SubtitleTypeMap.ToOptions(rows: rows);
            var reread = SubtitleTypeMap.FromOptions(configured: map);

            // Assert
            map.Should().BeEquivalentTo(
                new Dictionary<string, DlnaMedia> { [".txt"] = DlnaMedia.Video, [".lrc"] = DlnaMedia.Audio },
                "because an extension is stored the way file types are - lower case, with a leading dot");
            map.ContainsKey(".TXT").Should().BeTrue("because the map is looked up with whatever case a file on disc has");
            reread.Select(static r => (r.Extension, r.Kind)).Should().Equal(
                [(".lrc", DlnaMedia.Audio), (".txt", DlnaMedia.Video)],
                "because what was saved comes back as the same rows, in the editor's order");
        }

        [Test]
        public void Defaults_ReturnsAFreshSetEachTime()
        {
            // Arrange
            var first = SubtitleTypeMap.Defaults();
            first[0].Extension = ".changed";

            // Act
            var second = SubtitleTypeMap.Defaults();

            // Assert
            second.Should().NotContain(static r => r.Extension == ".changed",
                "because the editor mutates its rows, and an edit must never leak into the next restore");
            SubtitleFileExtensionDefaults.Create().Should().NotContainKey(".changed",
                "because the shipped list itself is never handed out to be edited");
        }
    }
}
