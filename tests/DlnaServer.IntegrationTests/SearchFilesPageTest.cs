using DlnaServer.Admin.Pages;
using DlnaServer.Core.Subtitles;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers what the search page does with a subtitle filter value <c>@bind</c> can hand it, and with
    /// what is typed into its number boxes.
    /// </summary>
    [TestFixture]
    internal sealed class SearchFilesPageTest
    {
        [Test]
        public void DefinedOrAny_ForANumberNoMemberHas_IsAny()
        {
            // Act
            var presence = SearchFiles.DefinedOrAny(value: (SubtitlePresence)99);

            // Assert
            presence.Should().BeNull(
                "because @bind parses a posted \"99\" into the enum, and an unnamed value used to reach a "
                + "switch that threw and ended the circuit");
        }

        [TestCase(SubtitlePresence.Yes)]
        [TestCase(SubtitlePresence.Embedded)]
        [TestCase(SubtitlePresence.Linked)]
        [TestCase(SubtitlePresence.No)]
        public void DefinedOrAny_ForANamedValue_KeepsIt(SubtitlePresence value)
        {
            // Act
            var presence = SearchFiles.DefinedOrAny(value: value);

            // Assert
            presence.Should().Be(value, "because every option the page offers is a named member");
        }

        [Test]
        public void DefinedOrAny_ForAny_StaysAny()
        {
            // Act
            var presence = SearchFiles.DefinedOrAny(value: null);

            // Assert
            presence.Should().BeNull("because the empty option means no subtitle filter");
        }

        [TestCase("1.5")]
        [TestCase("1e3")]
        [TestCase("-2")]
        [TestCase("12abc")]
        public void TryParseWholeNumber_ForAnythingButPlainDigits_Fails(string text)
        {
            // Act
            var parsed = SearchFiles.TryParseWholeNumber(text: text, value: out var value);

            // Assert
            parsed.Should().BeFalse("because a size or width that is not a whole number must stop the search, "
                + "not be dropped so the search runs without it");
            value.Should().BeNull("because nothing is read out of a value that did not parse");
            SearchFiles.WholeNumberError(text: text).Should().NotBeNull(
                "because the box has to say why nothing was searched");
        }

        [TestCase("15", 15)]
        [TestCase(" 0 ", 0)]
        public void TryParseWholeNumber_ForPlainDigits_ReadsThem(string text, int expected)
        {
            // Act
            var parsed = SearchFiles.TryParseWholeNumber(text: text, value: out var value);

            // Assert
            parsed.Should().BeTrue("because plain digits are a whole number");
            value.Should().Be(expected, "because the digits are the filter value");
            SearchFiles.WholeNumberError(text: text).Should().BeNull("because a valid value shows no error");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void TryParseWholeNumber_ForAnEmptyBox_IsNoFilter(string? text)
        {
            // Act
            var parsed = SearchFiles.TryParseWholeNumber(text: text, value: out var value);

            // Assert
            parsed.Should().BeTrue("because an empty box is a filter left unset, not a mistake");
            value.Should().BeNull("because an empty box filters on nothing");
        }
    }
}
