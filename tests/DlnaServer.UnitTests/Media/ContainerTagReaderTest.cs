using DlnaServer.Media.Processing.Metadata;

namespace DlnaServer.UnitTests.Media
{
    /// <summary>
    /// The tag block is whatever wrote the file decided to put there, so the reader is judged on what it
    /// does with input it did not expect as much as on the ordinary case.
    /// </summary>
    [TestFixture]
    internal sealed class ContainerTagReaderTest
    {
        [Test]
        public void Parse_ReadsTheContainerTags()
        {
            // Arrange
            const string json = """
                {
                  "format": {
                    "tags": {
                      "title": "My Song",
                      "artist": "Some Artist",
                      "album": "My Album",
                      "lyrics": "These are the lyrics..."
                    }
                  }
                }
                """;

            // Act
            var tags = ContainerTagReader.Parse(json);

            // Assert
            tags.Should().HaveCount(4, "because every tag in the block is kept");
            tags.Should().OnlyContain(t => t.StreamIndex == null,
                "because a tag under \"format\" describes the whole file, not one track");
            tags.Should().Contain(t => t.Name == "lyrics" && t.Value == "These are the lyrics...",
                "because lyrics are the reason the value column is long");
        }

        [Test]
        public void Parse_AttributesStreamTagsToTheirTrack()
        {
            // Arrange
            const string json = """
                {
                  "streams": [
                    { "index": 0, "tags": { "language": "eng" } },
                    { "index": 1, "tags": { "language": "ces", "title": "Commentary" } }
                  ],
                  "format": { "tags": { "album": "My Album" } }
                }
                """;

            // Act
            var tags = ContainerTagReader.Parse(json);

            // Assert
            tags.Should().HaveCount(4, "because both tracks' tags are kept alongside the container's");
            tags.Should().Contain(t => t.StreamIndex == 1 && t.Name == "title" && t.Value == "Commentary",
                "because a per-track tag has to be attributable to its track");
            tags.Should().Contain(t => t.StreamIndex == null && t.Name == "album",
                "because the container's own tags stay unattributed");
        }

        [Test]
        public void Parse_KeepsANonStringValueRatherThanFailingTheWholeFile()
        {
            // Arrange
            const string json = """
                { "format": { "tags": { "track": 7, "compilation": true, "artist": "Some Artist" } } }
                """;

            // Act
            var tags = ContainerTagReader.Parse(json);

            // Assert
            tags.Should().HaveCount(3,
                "because one oddly-typed tag must not cost the file every other tag it carries");
            tags.Should().Contain(t => t.Name == "track" && t.Value == "7",
                "because a numeric tag is still readable as text");
        }

        [Test]
        public void Parse_SkipsEmptyValuesAndNestedObjects()
        {
            // Arrange
            const string json = """
                {
                  "format": {
                    "tags": { "artist": "Some Artist", "comment": "   ", "extra": { "nested": "no" } }
                  }
                }
                """;

            // Act
            var tags = ContainerTagReader.Parse(json);

            // Assert
            tags.Should().ContainSingle("because a blank value and a nested object are both unshowable");
            tags[0].Name.Should().Be("artist", "because that is the only usable tag in the block");
        }

        [Test]
        public void Parse_TruncatesAValueToTheColumnLength()
        {
            // Arrange
            var oversized = new string('x', ContainerTagReader.MaxValueLength + 500);
            var json = $$"""{ "format": { "tags": { "lyrics": "{{oversized}}" } } }""";

            // Act
            var tags = ContainerTagReader.Parse(json);

            // Assert
            tags[0].Value.Length.Should().Be(ContainerTagReader.MaxValueLength,
                "because SQLite does not enforce the column length, so an oversized value would be "
                + "stored and only surprise a later reader");
        }

        [TestCase("", TestName = "Parse_ForEmptyInput_ReturnsNothing")]
        [TestCase("   ", TestName = "Parse_ForBlankInput_ReturnsNothing")]
        [TestCase("[1,2,3]", TestName = "Parse_ForANonObjectDocument_ReturnsNothing")]
        [TestCase("{}", TestName = "Parse_ForAnEmptyDocument_ReturnsNothing")]
        [TestCase("""{ "format": {} }""", TestName = "Parse_WhenThereIsNoTagBlock_ReturnsNothing")]
        public void Parse_ForInputCarryingNoTags_ReturnsNothing(string json)
        {
            // Arrange
            // Act
            var tags = ContainerTagReader.Parse(json);

            // Assert
            tags.Should().BeEmpty("because a file with nothing to say about itself is the common case");
        }
    }
}
