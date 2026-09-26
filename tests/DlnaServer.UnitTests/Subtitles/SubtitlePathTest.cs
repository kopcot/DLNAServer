using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Subtitles;

namespace DlnaServer.UnitTests.Subtitles
{
    /// <summary>
    /// Covers where a subtitle linked by hand may live - NOTES item 7d.
    /// </summary>
    [TestFixture]
    internal sealed class SubtitlePathTest
    {
        private static readonly string _mediaDirectory = Path.Combine(Path.GetTempPath(), "media", "Films");
        private static readonly IReadOnlyDictionary<string, DlnaMedia> _subtitleTypes = SubtitleFileExtensionDefaults.Create();

        [TestCase("film.en.srt", "film.en.srt")]
        [TestCase("Subs/film.srt", "Subs/film.srt")]
        [TestCase(@"Subs\film.srt", "Subs/film.srt")]
        [TestCase("  film.lrc  ", "film.lrc")]
        public void TryResolve_ForTheFolderOrOneBelow_Accepts(string input, string expected)
        {
            // Act
            var isUsable = SubtitlePath.TryResolve(
                mediaDirectory: _mediaDirectory,
                input: input,
                hiddenFolders: [],
                subtitleTypes: _subtitleTypes,
                relativePath: out var relativePath,
                fullPath: out var fullPath,
                problem: out _);

            // Assert
            isUsable.Should().BeTrue("because the file's own folder and one folder below it are allowed");
            relativePath.Should().Be(expected, "because the stored form uses forward slashes on every machine");
            fullPath.Should().StartWith(_mediaDirectory, "because the path is resolved from the media file's folder");
        }

        [TestCase("")]
        [TestCase("Subs/English/film.srt")]
        [TestCase("../film.srt")]
        [TestCase("./film.srt")]
        [TestCase("/etc/film.srt")]
        [TestCase("C:/film.srt")]
        [TestCase("film.txt")]
        [TestCase("film.srt.")]
        [TestCase("Subs /film.srt")]
        [TestCase("Subs./film.srt")]
        [TestCase("SUBS~1/film.srt")]
        public void TryResolve_ForAnythingElse_Refuses(string input)
        {
            // Act
            var isUsable = SubtitlePath.TryResolve(
                mediaDirectory: _mediaDirectory,
                input: input,
                hiddenFolders: [],
                subtitleTypes: _subtitleTypes,
                relativePath: out _,
                fullPath: out _,
                problem: out var problem);

            // Assert
            isUsable.Should().BeFalse("because only a subtitle in the folder or one below it may be linked");
            problem.Should().NotBeEmpty("because the page has to say why the path was refused");
        }

        [Test]
        public void TryResolve_ForAHiddenFolder_Refuses()
        {
            // Act
            var isUsable = SubtitlePath.TryResolve(
                mediaDirectory: _mediaDirectory,
                input: "Private/film.srt",
                hiddenFolders: ["Private"],
                subtitleTypes: _subtitleTypes,
                relativePath: out _,
                fullPath: out _,
                problem: out _);

            // Assert
            isUsable.Should().BeFalse("because a folder the library hides stays hidden for subtitles too");
        }

        [Test]
        public void TryResolve_ForATypeNoLongerListed_Refuses()
        {
            // Arrange
            var subtitleTypes = new Dictionary<string, DlnaMedia>(StringComparer.OrdinalIgnoreCase) { [".lrc"] = DlnaMedia.Audio };

            // Act
            var isUsable = SubtitlePath.TryResolve(
                mediaDirectory: _mediaDirectory,
                input: "film.srt",
                hiddenFolders: [],
                subtitleTypes: subtitleTypes,
                relativePath: out _,
                fullPath: out _,
                problem: out var problem);

            // Assert
            isUsable.Should().BeFalse("because serving re-checks this rule, and a link of an unlisted type must stop being served");
            problem.Should().Contain("Subtitle types", "because the operator has to be told where the list of types lives");
        }

        [Test]
        public void TryResolve_ForATypeTheOperatorAdded_Accepts()
        {
            // Arrange
            var subtitleTypes = new Dictionary<string, DlnaMedia>(StringComparer.OrdinalIgnoreCase) { [".txt"] = DlnaMedia.Video };

            // Act
            var isUsable = SubtitlePath.TryResolve(
                mediaDirectory: _mediaDirectory,
                input: "film.TXT",
                hiddenFolders: [],
                subtitleTypes: subtitleTypes,
                relativePath: out _,
                fullPath: out _,
                problem: out _);

            // Assert
            isUsable.Should().BeTrue("because a listed type is linkable by hand, whatever the case of its extension");
        }
    }
}
