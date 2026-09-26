using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Scanning;
using DlnaServer.Core.Dlna;
using DlnaServer.Media.Scanning;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.UnitTests.Media
{
    [TestFixture]
    internal sealed class LibraryScannerTest
    {
        private string _root = null!;
        private LibraryScanner _scanner = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Directory.CreateTempSubdirectory("dlna-scan-").FullName;
            _scanner = new LibraryScanner(NullLogger<LibraryScanner>.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, recursive: true);
        }

        [Test]
        public void EnumerateFiles_FindsMappedMediaFiles_AndIgnoresEverythingElse()
        {
            // Arrange
            CreateFile("movie.mkv", sizeInBytes: 2048);
            CreateFile("photo.jpg", sizeInBytes: 512);
            CreateFile("notes.txt", sizeInBytes: 10);
            CreateFile("archive.zip", sizeInBytes: 10);

            // Act
            var found = _scanner.EnumerateFiles(CreateOptions(), CancellationToken.None).ToArray();

            // Assert
            found.Select(static f => f.FileName).Should().BeEquivalentTo(
                ["movie.mkv", "photo.jpg"],
                "because an extension is media when configuration names it or the built-in catalog knows "
                + "it, and neither carries .txt or .zip");
        }

        [Test]
        public void EnumerateFiles_PopulatesMimeProfileAndContentStamp()
        {
            // Arrange
            CreateFile("movie.mkv", sizeInBytes: 2048);

            // Act
            var file = _scanner.EnumerateFiles(CreateOptions(), CancellationToken.None).Single();

            // Assert
            file.Mime.Should().Be(DlnaMime.VideoXMatroska, "because the mapping says .mkv is Matroska");
            file.DlnaProfileName.Should().Be("MATROSKA", "because the mapping supplied the DLNA profile");
            file.SizeInBytes.Should().Be(2048, "because the stamp and the item size both come from the file");
            file.ContentStamp.Should().StartWith("2048:",
                "because the stamp is size and modification time, so a changed file is detectable");
            file.Extension.Should().Be(".mkv", "because extensions are normalised to lower case");
        }

        /// <summary>
        /// The reference matched exclusions as a substring of the whole path, so excluding
        /// <c>@Recycle</c> also hid a folder named <c>My@RecycleShow</c>.
        /// </summary>
        [Test]
        public void EnumerateFiles_ExcludesBySegment_NotBySubstring()
        {
            // Arrange
            CreateFile(Path.Combine("@Recycle", "deleted.mkv"), sizeInBytes: 100);
            CreateFile(Path.Combine("My@RecycleShow", "keep.mkv"), sizeInBytes: 100);

            // Act
            var found = _scanner.EnumerateFiles(CreateOptions(), CancellationToken.None).ToArray();

            // Assert
            found.Select(static f => f.FileName).Should().BeEquivalentTo(
                ["keep.mkv"],
                "because only a whole path segment equal to '@Recycle' is excluded");
        }

        [Test]
        public void EnumerateFiles_SkipsZeroLengthFiles()
        {
            // Arrange
            CreateFile("empty.mkv", sizeInBytes: 0);
            CreateFile("real.mkv", sizeInBytes: 64);

            // Act
            var found = _scanner.EnumerateFiles(CreateOptions(), CancellationToken.None).ToArray();

            // Assert
            found.Select(static f => f.FileName).Should().BeEquivalentTo(
                ["real.mkv"],
                "because a zero-length file is either still being copied or broken");
        }

        [Test]
        public void EnumerateFiles_WhenASourceFolderIsMissing_StillScansTheOthers()
        {
            // Arrange
            CreateFile("movie.mkv", sizeInBytes: 100);
            var options = CreateOptions() with
            {
                SourceFolders = [Path.Combine(_root, "does-not-exist"), _root],
            };

            // Act
            var found = _scanner.EnumerateFiles(options, CancellationToken.None).ToArray();

            // Assert
            found.Should().HaveCount(1,
                "because a missing source folder is a configuration problem, not a reason to index nothing");
        }

        [Test]
        public void EnumerateDirectories_ReturnsSourceRootAndSubdirectories()
        {
            // Arrange
            CreateFile(Path.Combine("movies", "action", "film.mkv"), sizeInBytes: 100);

            // Act
            var found = _scanner.EnumerateDirectories(CreateOptions(), CancellationToken.None).ToArray();

            // Assert
            found.Should().BeEquivalentTo(
                [
                    _root,
                    Path.Combine(_root, "movies"),
                    Path.Combine(_root, "movies", "action"),
                ],
                "because the source root is itself a container and every level below it is browsable");
        }

        /// <summary>
        /// A folder with nothing playable in it is not a container worth offering, and a volume-wide walk
        /// found thousands of them - QNAP's <c>.streams</c> alone is 67 folders holding no media.
        /// </summary>
        [Test]
        public void EnumerateDirectories_OmitsAFolderWithNoMediaBeneathIt()
        {
            // Arrange
            CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);
            _ = Directory.CreateDirectory(Path.Combine(_root, ".streams", "02", "CB"));

            // Act
            var found = _scanner.EnumerateDirectories(CreateOptions(), CancellationToken.None).ToArray();

            // Assert
            found.Should().NotContain(
                Path.Combine(_root, ".streams"),
                "because indexing a folder no media can be reached through only puts it in front of a "
                + "television, and its 66 subfolders with it");
        }

        /// <summary>
        /// A folder of subtitles, artwork or notes holds files but no <i>media</i>, so the extension
        /// mapping is what decides, not the presence of anything at all.
        /// </summary>
        [Test]
        public void EnumerateDirectories_OmitsAFolderHoldingOnlyUnmappedFiles()
        {
            // Arrange
            CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);
            CreateFile(Path.Combine("extras", "notes.txt"), sizeInBytes: 10);

            // Act
            var found = _scanner.EnumerateDirectories(CreateOptions(), CancellationToken.None).ToArray();

            // Assert
            found.Should().NotContain(
                Path.Combine(_root, "extras"),
                "because a folder is browsable only when something in it can be played or shown");
        }

        /// <summary>
        /// Every folder on the way to a file has to be yielded, or the file has no path a renderer can
        /// walk down to reach it.
        /// </summary>
        [Test]
        public void EnumerateDirectories_ReturnsEveryAncestorOfAMediaFile()
        {
            // Arrange
            CreateFile(Path.Combine("movies", "2024", "action", "film.mkv"), sizeInBytes: 100);

            // Act
            var found = _scanner.EnumerateDirectories(CreateOptions(), CancellationToken.None).ToArray();

            // Assert
            found.Should().BeEquivalentTo(
                [
                    _root,
                    Path.Combine(_root, "movies"),
                    Path.Combine(_root, "movies", "2024"),
                    Path.Combine(_root, "movies", "2024", "action"),
                ],
                "because the three folders above the film are empty of media themselves and still have to "
                + "be there for anything to browse through");
        }

        /// <summary>
        /// The root is the library's entry point, so it is offered even before anything is in it -
        /// otherwise a fresh deployment has nothing at all and no way to tell why.
        /// </summary>
        [Test]
        public void EnumerateDirectories_ReturnsTheSourceRootWithNoMediaAtAll()
        {
            // Act
            var found = _scanner.EnumerateDirectories(CreateOptions(), CancellationToken.None).ToArray();

            // Assert
            found.Should().Equal([_root],
                "because the configured folder is the library, whether or not it holds anything yet");
        }

        [Test]
        public void EnumerateDirectories_OmitsExcludedFolders()
        {
            // Arrange
            CreateFile(Path.Combine("@Recycle", "gone.mkv"), sizeInBytes: 100);
            CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);

            // Act
            var found = _scanner.EnumerateDirectories(CreateOptions(), CancellationToken.None).ToArray();

            // Assert
            found.Should().NotContain(
                Path.Combine(_root, "@Recycle"),
                "because an excluded folder must not become a browsable container");
        }

        /// <summary>
        /// Enumeration must be lazy: a 20,000-file library cannot be held in one list.
        /// </summary>
        [Test]
        public void EnumerateFiles_IsLazy_AndDoesNotWalkUntilEnumerated()
        {
            // Arrange
            CreateFile("movie.mkv", sizeInBytes: 100);

            // Act
            var sequence = _scanner.EnumerateFiles(CreateOptions(), CancellationToken.None);
            var firstOnly = sequence.Take(1).ToArray();

            // Assert
            firstOnly.Should().HaveCount(1,
                "because the caller can stop after one item without the scanner materialising the rest");
        }

        /// <summary>
        /// An extension configuration does not name is still media when the catalog knows it.
        /// </summary>
        /// <remarks>
        /// The fallback tier, which for a long time was described by three separate doc comments and
        /// implemented by nothing: <c>.webm</c> and <c>.flac</c> are in the catalog, so a library holding
        /// them was silently not indexed unless the operator had typed the extension into
        /// <c>config.json</c> - and nothing said so.
        /// </remarks>
        [TestCase("clip.webm", DlnaMime.VideoWebm)]
        [TestCase("song.flac", DlnaMime.AudioFlac)]
        [TestCase("stream.ts", DlnaMime.VideoMp2T)]
        public void EnumerateFiles_ForAnExtensionOnlyTheCatalogKnows_TakesTheCatalogMime(
            string fileName,
            DlnaMime expected)
        {
            // Arrange
            CreateFile(fileName, sizeInBytes: 1024);

            // Act
            var file = _scanner.EnumerateFiles(CreateOptions(), CancellationToken.None).Single();

            // Assert
            file.Mime.Should().Be(expected,
                $"because configuration names no mapping for '{fileName}' and the catalog does");
            file.DlnaProfileName.Should().Be(expected.ToMainProfileName(),
                "because the inferred mapping carries the MIME's own main profile, which is what "
                + "res@protocolInfo advertises");
        }

        /// <summary>
        /// The fallback only infers kinds a renderer can be offered, so a subtitle is not a library item.
        /// </summary>
        /// <remarks>
        /// The catalog knows <c>.srt</c>, <c>.vtt</c> and <c>.sub</c> perfectly well. Indexing them would
        /// put every sidecar subtitle beside the films as a <c>Generic</c> item on the television, which
        /// is a regression rather than a feature - and the reference's own subtitle support is marked as
        /// not implemented.
        /// <para>
        /// Since 2026-09-26 the walk does report them - flagged as subtitles, so the indexer can link each
        /// to its media - and this still pins the half that matters: never as media.
        /// </para>
        /// </remarks>
        [TestCase("film.srt")]
        [TestCase("film.vtt")]
        [TestCase("film.sub")]
        [TestCase("film.lrc")]
        public void EnumerateFiles_ForASubtitle_ReportsItAsASubtitleAndNeverAsMedia(string fileName)
        {
            // Arrange
            CreateFile(fileName, sizeInBytes: 128);

            // Act
            var found = _scanner.EnumerateFiles(CreateOptions(), CancellationToken.None).ToArray();

            // Assert
            found.Should().ContainSingle($"because '{fileName}' is reported so it can be linked to its media")
                .Which.IsSubtitle.Should().BeTrue(
                    $"because '{fileName}' is a subtitle, and the fallback infers only video, audio and images");
        }

        /// <summary>
        /// Configuration still wins outright, which is what keeps the device quirks working.
        /// </summary>
        /// <remarks>
        /// The shipped mapping sends <c>.mp3</c> as <c>audio/mp4</c> because that is what LG televisions
        /// accept, where the catalog says <c>audio/mpeg3</c>. A fallback that could override configuration
        /// would undo exactly the one mapping the project documents as a workaround.
        /// </remarks>
        [Test]
        public void EnumerateFiles_ForAnExtensionBothTiersKnow_TakesTheConfiguredMime()
        {
            // Arrange
            CreateFile("song.mp3", sizeInBytes: 1024);

            var options = CreateOptions() with
            {
                ExtensionMappings = new Dictionary<string, MediaExtensionMapping>(StringComparer.OrdinalIgnoreCase)
                {
                    [".mp3"] = new(DlnaMime.AudioMp4, "MP4"),
                },
            };

            // Act
            var file = _scanner.EnumerateFiles(options, CancellationToken.None).Single();

            // Assert
            file.Mime.Should().Be(DlnaMime.AudioMp4,
                "because configuration is where device quirks live and the catalog's audio/mpeg3 must not "
                + "displace them");
        }

        private LibraryScanRequest CreateOptions()
        {
            return new LibraryScanRequest
            {
                SourceFolders = [_root],
                ExcludedFolderNames = ["@Recycle", ".@__thumb"],
                ExtensionMappings = new Dictionary<string, MediaExtensionMapping>(StringComparer.OrdinalIgnoreCase)
                {
                    [".mkv"] = new(DlnaMime.VideoXMatroska, "MATROSKA"),
                    [".jpg"] = new(DlnaMime.ImageJpeg, "JPEG"),
                },
            };
        }

        private void CreateFile(string relativePath, int sizeInBytes)
        {
            var fullPath = Path.Combine(_root, relativePath);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, new byte[sizeInBytes]);
        }
    }
}
