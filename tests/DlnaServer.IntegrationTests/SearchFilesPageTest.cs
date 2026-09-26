using DlnaServer.Admin.Pages;
using DlnaServer.Core.Subtitles;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers what the search page does with a subtitle filter value <c>@bind</c> can hand it.
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
    }
}
