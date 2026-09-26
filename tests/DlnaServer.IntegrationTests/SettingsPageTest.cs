using DlnaServer.Admin.Pages;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the subtitle types the Settings page names after a save that removed some.
    /// </summary>
    [TestFixture]
    internal sealed class SettingsPageTest
    {
        [Test]
        public void RemovedSubtitleTypes_NamesOnlyTheExtensionsTheSaveDropped()
        {
            // Act
            var removed = Settings.RemovedSubtitleTypes(
                previous: [".srt", ".vtt", ".lrc", ".ass"],
                saved: [".SRT", ".ass", ".smi"]);

            // Assert
            removed.Should().Equal([".lrc", ".vtt"],
                "because removing a type deletes its links at the next scan, hand-added ones included, so the "
                + "operator is told which - sorted, case-insensitively, and never an extension that was added");
        }

        [Test]
        public void RemovedSubtitleTypes_WhenNothingWasDropped_IsEmpty()
        {
            // Act
            var removed = Settings.RemovedSubtitleTypes(previous: [".srt"], saved: [".srt", ".vtt"]);

            // Assert
            removed.Should().BeEmpty("because only adding a type deletes nothing");
        }
    }
}
