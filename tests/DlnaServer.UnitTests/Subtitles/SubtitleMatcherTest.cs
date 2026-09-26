using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Subtitles;

namespace DlnaServer.UnitTests.Subtitles
{
    /// <summary>
    /// Covers which subtitle file belongs to which media file - NOTES items 7, 7a, 7b and 7h.
    /// </summary>
    [TestFixture]
    internal sealed class SubtitleMatcherTest
    {
        private static readonly IReadOnlyDictionary<string, DlnaMedia> _subtitleTypes = SubtitleFileExtensionDefaults.Create();

        [TestCase("video.srt")]
        [TestCase("video.en.srt")]
        [TestCase("video.en01.srt")]
        [TestCase("video.1.srt")]
        [TestCase("video.1.en.srt")]
        [TestCase("VIDEO.EN.SRT")]
        public void Match_ForTheNamesTheOperatorListed_LinksToTheVideo(string subtitle)
        {
            // Arrange
            SubtitleMedia<string>[] media = [new("video", "video.mpg", DlnaMedia.Video)];

            // Act
            var matches = SubtitleMatcher.Match(media, [subtitle], subtitleTypes: _subtitleTypes);

            // Assert
            matches.Should().ContainSingle(
                "because a subtitle named like the video, with or without a short suffix, belongs to it")
                .Which.Key.Should().Be("video",
                    "because there is only one video it can belong to");
        }

        [Test]
        public void Match_WhenALongerMediaNameFits_PrefersIt()
        {
            // Arrange
            SubtitleMedia<string>[] media =
            [
                new("video", "video.mkv", DlnaMedia.Video),
                new("video.1", "video.1.mkv", DlnaMedia.Video),
            ];

            // Act
            var matches = SubtitleMatcher.Match(media, ["video.1.en.srt", "video.en.srt"], subtitleTypes: _subtitleTypes);

            // Assert
            matches.Should().BeEquivalentTo(
                [
                    new SubtitleMatch<string>("video.1", "video.1.en.srt", "en"),
                    new SubtitleMatch<string>("video", "video.en.srt", "en"),
                ],
                "because rule 7b gives a longer name to the other media file in the folder that it names exactly");
        }

        [Test]
        public void Match_WhenTheLongerNameIsMusic_StillGivesTheSubtitleToTheVideo()
        {
            // Arrange
            SubtitleMedia<string>[] media =
            [
                new("video", "video.mkv", DlnaMedia.Video),
                new("song", "video.1.mp3", DlnaMedia.Audio),
            ];

            // Act
            var matches = SubtitleMatcher.Match(media, ["video.1.en.srt"], subtitleTypes: _subtitleTypes);

            // Assert
            matches.Should().ContainSingle("because a subtitle can only belong to a video")
                .Which.Key.Should().Be("video",
                    "because the music file's longer name must not take a subtitle it cannot use");
        }

        [Test]
        public void Match_ForTwoVideosWithTheSameName_LinksBoth()
        {
            // Arrange
            SubtitleMedia<string>[] media =
            [
                new("mkv", "film.mkv", DlnaMedia.Video),
                new("mp4", "film.mp4", DlnaMedia.Video),
            ];

            // Act
            var matches = SubtitleMatcher.Match(media, ["film.srt"], subtitleTypes: _subtitleTypes);

            // Assert
            matches.Select(static m => m.Key).Should().BeEquivalentTo(["mkv", "mp4"],
                "because the subtitle fits both copies of the film equally");
        }

        [Test]
        public void Match_ForLyrics_LinksOnlyToMusic()
        {
            // Arrange
            SubtitleMedia<string>[] media =
            [
                new("song", "song.mp3", DlnaMedia.Audio),
                new("clip", "song.mp4", DlnaMedia.Video),
            ];

            // Act
            var matches = SubtitleMatcher.Match(media, ["song.lrc", "song.srt"], subtitleTypes: _subtitleTypes);

            // Assert
            matches.Should().BeEquivalentTo(
                [
                    new SubtitleMatch<string>("song", "song.lrc", null),
                    new SubtitleMatch<string>("clip", "song.srt", null),
                ],
                "because lyrics belong to the music and a subtitle to the video");
        }

        [Test]
        public void Match_ForATypeTheOperatorAdded_LinksIt()
        {
            // Arrange
            SubtitleMedia<string>[] media = [new("film", "film.mkv", DlnaMedia.Video)];
            var subtitleTypes = new Dictionary<string, DlnaMedia>(StringComparer.OrdinalIgnoreCase) { [".txt"] = DlnaMedia.Video };

            // Act
            var matches = SubtitleMatcher.Match(media: media, fileNames: ["film.en.txt"], subtitleTypes: subtitleTypes);

            // Assert
            matches.Should().ContainSingle("because .txt is listed as a subtitle type for video")
                .Which.Should().Be(new SubtitleMatch<string>("film", "film.en.txt", "en"),
                    "because an added type is matched and read for a language exactly like a built-in one");
        }

        [Test]
        public void Match_ForATypeTheOperatorRemoved_LinksNothing()
        {
            // Arrange
            SubtitleMedia<string>[] media = [new("film", "film.mkv", DlnaMedia.Video)];
            var subtitleTypes = SubtitleFileExtensionDefaults.Create();
            _ = subtitleTypes.Remove(".srt");

            // Act
            var matches = SubtitleMatcher.Match(media: media, fileNames: ["film.srt"], subtitleTypes: subtitleTypes);

            // Assert
            matches.Should().BeEmpty("because a type no longer listed is not a subtitle any more, however well it is named");
        }

        [Test]
        public void Match_ForATypeListedForMusic_LinksItToTheSongAndNotTheVideo()
        {
            // Arrange
            SubtitleMedia<string>[] media =
            [
                new("song", "song.mp3", DlnaMedia.Audio),
                new("clip", "song.mp4", DlnaMedia.Video),
            ];
            var subtitleTypes = new Dictionary<string, DlnaMedia>(StringComparer.OrdinalIgnoreCase) { [".txt"] = DlnaMedia.Audio };

            // Act
            var matches = SubtitleMatcher.Match(media: media, fileNames: ["song.txt"], subtitleTypes: subtitleTypes);

            // Assert
            matches.Should().ContainSingle("because one file matched one kind of media")
                .Which.Key.Should().Be("song", "because the configured kind decides what a type goes with, not its extension");
        }

        [Test]
        public void Match_ForASubWithAnIdxBesideIt_LinksNothing()
        {
            // Arrange
            SubtitleMedia<string>[] media = [new("film", "film.mkv", DlnaMedia.Video)];

            // Act
            var matches = SubtitleMatcher.Match(media, ["film.sub", "film.idx"], subtitleTypes: _subtitleTypes);

            // Assert
            matches.Should().BeEmpty("because a .sub beside an .idx is VobSub pictures, which no television reads as text");
        }

        [Test]
        public void Match_ForANameThatOnlyStartsLikeTheVideo_LinksNothing()
        {
            // Arrange
            SubtitleMedia<string>[] media = [new("video", "video.mkv", DlnaMedia.Video)];

            // Act
            var matches = SubtitleMatcher.Match(media, ["video2.srt", "videos.en.srt"], subtitleTypes: _subtitleTypes);

            // Assert
            matches.Should().BeEmpty("because the video's name has to be followed by a dot or nothing at all");
        }

        [TestCase(".en", "en")]
        [TestCase(".EN", "en")]
        [TestCase(".eng", "eng")]
        [TestCase(".en01", "en")]
        [TestCase(".en.1", "en")]
        [TestCase(".en.forced", "en")]
        [TestCase(".cz.sdh", "cz")]
        [TestCase(".1", null)]
        [TestCase(".extended", null)]
        [TestCase(".Extended.Cut", null)]
        [TestCase(".HD", null)]
        [TestCase("", null)]
        public void GuessLanguage_ReadsTheLanguageWord(string suffix, string? expected)
        {
            // Act
            var language = SubtitleMatcher.GuessLanguage(suffix);

            // Assert
            language.Should().Be(expected,
                "because the last two- or three-letter word, past the descriptive ones, names the language");
        }

        [TestCase("film.en.srt", "film.mkv", "en")]
        [TestCase("Subs/film.forced.cz.srt", "film.mkv", "cz")]
        [TestCase("other.en.srt", "film.mkv", null)]
        [TestCase("filmen.srt", "film.mkv", null)]
        public void GuessLanguage_ForAMediaFile_ReadsOnlyANameThatIsTheMediasOwn(
            string subtitleFileName,
            string mediaFileName,
            string? expected)
        {
            // Act
            var language = SubtitleMatcher.GuessLanguage(subtitleFileName, mediaFileName);

            // Assert
            language.Should().Be(expected,
                "because a name that does not start with the media file's own gives the guess nothing to stand on");
        }

        [Test]
        public void Match_ReadsTheLanguageOnlyFromWhatFollowsTheVideoName()
        {
            // Arrange
            SubtitleMedia<string>[] media = [new("office", "The.Office.US.mkv", DlnaMedia.Video)];

            // Act
            var matches = SubtitleMatcher.Match(media, ["The.Office.US.srt"], subtitleTypes: _subtitleTypes);

            // Assert
            matches.Should().ContainSingle("because the subtitle is named exactly like the video")
                .Which.Language.Should().BeNull("because US is part of the video's own name, not a language");
        }
    }
}
