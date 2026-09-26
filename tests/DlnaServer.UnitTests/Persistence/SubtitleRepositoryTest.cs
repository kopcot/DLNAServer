using DlnaServer.Core.Configuration;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Subtitles;
using DlnaServer.Persistence;
using DlnaServer.Persistence.Entities;
using DlnaServer.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DlnaServer.UnitTests.Persistence
{
    /// <summary>
    /// Covers which links a scan keeps, drops and brings back - the lifecycle rules the review of
    /// 2026-09-26 found three holes in.
    /// </summary>
    [TestFixture]
    internal sealed class SubtitleRepositoryTest
    {
        // Built with the platform's separator, the way the scanner reports folders.
        private static readonly string _films = Path.Combine(Path.GetTempPath(), "dlna-subtitle-test", "Films");
        private static readonly IReadOnlyDictionary<string, DlnaMedia> _subtitleTypes = SubtitleFileExtensionDefaults.Create();

        private SqliteTestDatabase _database = null!;

        [SetUp]
        public void SetUp()
        {
            _database = new SqliteTestDatabase();
        }

        [TearDown]
        public void TearDown()
        {
            _database.Dispose();
        }

        [Test]
        public async Task SyncAutomaticAsync_ForANameMatch_LinksItWithItsLanguage()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var film = await AddFilmAsync(context);
            var subtitles = new SubtitleRepository(context);

            // Act
            var (added, _) = await subtitles.SyncAutomaticAsync(
                fileNamesByDirectory: Seen(_films, "film.en.srt"),
                excludedFolders: [],
                subtitleTypes: _subtitleTypes,
                isDefinitelyAbsent: static _ => false,
                cancellationToken: CancellationToken.None);

            // Assert
            added.Should().Be(1,
                "because film.en.srt is named after film.mkv");
            (await LinksOfAsync(subtitles, film)).Should().ContainSingle(
                    "because the one match is linked")
                .Which.Language.Should().Be("en",
                    "because the language is read from what follows the film's name");
        }

        /// <summary>
        /// A manual link replaces the automatic ones even when they still match, so a scan that read the
        /// table before the operator pressed Add cannot leave both sets behind for good.
        /// </summary>
        [Test]
        public async Task SyncAutomaticAsync_WhenTheMediaHasAManualLink_DropsTheAutomaticOneThatStillMatches()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var film = await AddFilmAsync(context);
            await AddLinkAsync(context, film, relativePath: "film.en.srt", source: SubtitleSource.Automatic);
            await AddLinkAsync(context, film, relativePath: "chosen.srt", source: SubtitleSource.Manual);
            var subtitles = new SubtitleRepository(context);

            // Act
            _ = await subtitles.SyncAutomaticAsync(
                fileNamesByDirectory: Seen(_films, "film.en.srt", "chosen.srt"),
                excludedFolders: [],
                subtitleTypes: _subtitleTypes,
                isDefinitelyAbsent: static _ => false,
                cancellationToken: CancellationToken.None);

            // Assert
            (await LinksOfAsync(subtitles, film)).Select(static l => l.RelativePath).Should().Equal(["chosen.srt"],
                "because a television must never be offered the hand-picked subtitle and the automatic ones together");
        }

        [Test]
        public async Task RemoveAsync_OfAHandAddedLinkThatAlsoMatches_StaysRemovedAfterAScan()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var film = await AddFilmAsync(context);
            var subtitles = new SubtitleRepository(context);
            _ = await subtitles.AddManualAsync(
                mediaFilePublicId: film.PublicId,
                relativePath: "film.en.srt",
                language: null,
                cancellationToken: CancellationToken.None);
            var link = (await LinksOfAsync(subtitles, film)).Single();

            // Act
            await subtitles.RemoveAsync(publicId: link.PublicId, cancellationToken: CancellationToken.None);
            _ = await subtitles.SyncAutomaticAsync(
                fileNamesByDirectory: Seen(_films, "film.en.srt"),
                excludedFolders: [],
                subtitleTypes: _subtitleTypes,
                isDefinitelyAbsent: static _ => false,
                cancellationToken: CancellationToken.None);

            // Assert
            (await LinksOfAsync(subtitles, film)).Should().BeEmpty(
                "because the operator removed it, and a name match is no reason to link it straight back");
        }

        [Test]
        public async Task SyncAutomaticAsync_ForALinkIntoAFolderExcludedSince_DropsItWithoutLookingAtTheDisc()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var film = await AddFilmAsync(context);
            var subtitles = new SubtitleRepository(context);
            _ = await subtitles.SyncAutomaticAsync(
                fileNamesByDirectory: Seen(Path.Combine(_films, "Subs"), "film.de.srt"),
                excludedFolders: [],
                subtitleTypes: _subtitleTypes,
                isDefinitelyAbsent: static _ => false,
                cancellationToken: CancellationToken.None);
            var probes = 0;

            // Act - the scan now skips Subs, so it reports nothing from there.
            _ = await subtitles.SyncAutomaticAsync(
                fileNamesByDirectory: new Dictionary<string, HashSet<string>>(StringComparer.Ordinal),
                excludedFolders: ["Subs"],
                subtitleTypes: _subtitleTypes,
                isDefinitelyAbsent: _ =>
                {
                    probes++;

                    return false;
                },
                cancellationToken: CancellationToken.None);

            // Assert
            (await LinksOfAsync(subtitles, film)).Should().BeEmpty(
                "because a folder the library skips is excluded for subtitles too");
            probes.Should().Be(0,
                "because the scan never walks an excluded folder, so looking at the disc could change nothing and wakes it");
        }

        [Test]
        public async Task SyncAutomaticAsync_ForARemovedMarkerWhoseFileIsGone_DropsTheMarker()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var film = await AddFilmAsync(context);
            await AddLinkAsync(context, film, relativePath: "film.en.srt", source: SubtitleSource.Removed);
            var subtitles = new SubtitleRepository(context);

            // Act
            var (_, dropped) = await subtitles.SyncAutomaticAsync(
                fileNamesByDirectory: new Dictionary<string, HashSet<string>>(StringComparer.Ordinal),
                excludedFolders: [],
                subtitleTypes: _subtitleTypes,
                isDefinitelyAbsent: static _ => true,
                cancellationToken: CancellationToken.None);

            // Assert
            dropped.Should().Be(1,
                "because a removed marker only exists to keep its file out, and the file is gone");
            (await context.SubtitleFiles.CountAsync(CancellationToken.None)).Should().Be(0,
                "because nothing is left for the marker to stand for");
        }

        [Test]
        public async Task SyncAutomaticAsync_ForAHandAddedLinkWhoseFileIsGone_DropsIt()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var film = await AddFilmAsync(context);
            await AddLinkAsync(context, film, relativePath: "chosen.srt", source: SubtitleSource.Manual);
            var subtitles = new SubtitleRepository(context);

            // Act
            _ = await subtitles.SyncAutomaticAsync(
                fileNamesByDirectory: new Dictionary<string, HashSet<string>>(StringComparer.Ordinal),
                excludedFolders: [],
                subtitleTypes: _subtitleTypes,
                isDefinitelyAbsent: static _ => true,
                cancellationToken: CancellationToken.None);

            // Assert
            (await LinksOfAsync(subtitles, film)).Should().BeEmpty(
                "because a link to a file that is gone would only ever answer a television with a 404");
        }

        [Test]
        public async Task SyncAutomaticAsync_ForAHandAddedLinkIntoAnExcludedFolder_DropsIt()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var film = await AddFilmAsync(context);
            await AddLinkAsync(context, film, relativePath: "Subs/chosen.srt", source: SubtitleSource.Manual);
            var subtitles = new SubtitleRepository(context);

            // Act
            _ = await subtitles.SyncAutomaticAsync(
                fileNamesByDirectory: new Dictionary<string, HashSet<string>>(StringComparer.Ordinal),
                excludedFolders: ["Subs"],
                subtitleTypes: _subtitleTypes,
                isDefinitelyAbsent: static _ => false,
                cancellationToken: CancellationToken.None);

            // Assert
            (await LinksOfAsync(subtitles, film)).Should().BeEmpty(
                "because a hand-added link may not point into a folder the library skips, however it got there");
        }

        /// <summary>
        /// A type taken off the list is refused wherever a link is served, and the scan stops reporting its
        /// files - so without this the links would stay listed for televisions and never work.
        /// </summary>
        [Test]
        public async Task SyncAutomaticAsync_ForLinksOfATypeNoLongerListed_DropsThemButKeepsARemovedMarker()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var film = await AddFilmAsync(context);
            await AddLinkAsync(context, film, relativePath: "film.en.srt", source: SubtitleSource.Automatic);
            await AddLinkAsync(context, film, relativePath: "chosen.srt", source: SubtitleSource.Manual);
            await AddLinkAsync(context, film, relativePath: "film.de.srt", source: SubtitleSource.Removed);
            await AddLinkAsync(context, film, relativePath: "film.vtt", source: SubtitleSource.Automatic);
            var subtitleTypes = SubtitleFileExtensionDefaults.Create();
            _ = subtitleTypes.Remove(".srt");
            var subtitles = new SubtitleRepository(context);

            // Act - the scan no longer reports the .srt files, and they are all still on disc.
            _ = await subtitles.SyncAutomaticAsync(
                fileNamesByDirectory: Seen(_films, "film.vtt"),
                excludedFolders: [],
                subtitleTypes: subtitleTypes,
                isDefinitelyAbsent: static _ => false,
                cancellationToken: CancellationToken.None);

            // Assert
            (await LinksOfAsync(subtitles, film)).Select(static l => l.RelativePath).Should().Equal(["film.vtt"],
                "because a link of an unlisted type, found by name or added by hand, can no longer be served, "
                + "and the hand-added one going must not keep the still-listed automatic link away");
            (await context.SubtitleFiles.CountAsync(s => s.Source == SubtitleSource.Removed, CancellationToken.None)).Should().Be(1,
                "because a removed marker is harmless, and keeps the file out if the type is listed again");
        }

        [TestCase("  EN  ", "en")]
        [TestCase("", null)]
        [TestCase("abcdefghijklmnopqrstuvwxyz0123456789", "abcdefghijklmnopqrstuvwxyz012345")]
        public async Task SetLanguageAsync_StoresTheLanguageTrimmedLoweredAndShortened(string input, string? expected)
        {
            // Arrange
            await using var context = _database.CreateContext();
            var film = await AddFilmAsync(context);
            await AddLinkAsync(context, film, relativePath: "film.srt", source: SubtitleSource.Manual);
            var subtitles = new SubtitleRepository(context);
            var link = (await LinksOfAsync(subtitles, film)).Single();

            // Act
            await subtitles.SetLanguageAsync(
                publicId: link.PublicId,
                language: input,
                cancellationToken: CancellationToken.None);

            // Assert
            (await subtitles.GetByPublicIdAsync(publicId: link.PublicId, cancellationToken: CancellationToken.None))!
                .Language.Should().Be(expected,
                    "because the page stores what the operator typed in one form, with blank meaning unknown");
        }

        [Test]
        public async Task LanguageSearch_ForALinkedFilesLanguage_OffersItAndFindsTheFilm()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var film = await AddFilmAsync(context);
            await AddLinkAsync(context, film, relativePath: "film.srt", source: SubtitleSource.Manual);
            var subtitles = new SubtitleRepository(context);
            var link = (await LinksOfAsync(subtitles, film)).Single();
            await subtitles.SetLanguageAsync(publicId: link.PublicId, language: "cs", cancellationToken: CancellationToken.None);
            var files = CreateFileRepository(context);

            // Act
            var languages = await files.GetLanguagesAsync(CancellationToken.None);
            var found = await files.SearchAsync(
                new MediaFileSearchRequest { SubtitleLanguages = ["cs"] },
                CancellationToken.None);

            // Assert
            languages.Subtitle.Should().Contain("cs",
                "because a language the operator set on a linked file is one the search can match");
            found.Select(static f => f.PublicId).Should().Equal([film.PublicId],
                "because a film whose only subtitle is a linked file is still found by its language");
        }

        /// <summary>
        /// The admin page catches only <see cref="System.Data.Common.DbException"/>, and its circuit keeps
        /// this context for its whole life - so a failed add must surface as one and leave nothing tracked.
        /// </summary>
        [Test]
        public async Task AddManualAsync_WhenTheSaveFails_ThrowsTheProviderExceptionAndForgetsTheRow()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var film = await AddFilmAsync(context);
            var subtitles = new SubtitleRepository(context);
            var mediaFileId = await context.Files
                .Where(f => f.PublicId == film.PublicId)
                .Select(static f => f.Id)
                .SingleAsync(CancellationToken.None);

            // Pending in the tracker but not in the table, so the repository's lookup misses it and the
            // save inserts the same link twice against its unique index.
            _ = context.SubtitleFiles.Add(new SubtitleFileEntity
            {
                MediaFileId = mediaFileId,
                RelativePath = "film.en.srt",
                Source = SubtitleSource.Automatic,
            });

            // Act
            var act = () => subtitles.AddManualAsync(
                mediaFilePublicId: film.PublicId,
                relativePath: "film.en.srt",
                language: null,
                cancellationToken: CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<System.Data.Common.DbException>(
                "because EF's DbUpdateException does not derive from DbException, and the page outside "
                + "Persistence cannot name the EF type to catch it");
            context.ChangeTracker.Entries().Should().BeEmpty(
                "because a row left tracked by the failed save would be re-submitted by the circuit's next write");
        }

        private static Dictionary<string, HashSet<string>> Seen(string directory, params string[] fileNames)
        {
            return new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                [directory] = new HashSet<string>(fileNames, StringComparer.Ordinal),
            };
        }

        private static async Task<MediaFileDto> AddFilmAsync(DlnaDbContext context)
        {
            var directories = new MediaDirectoryRepository(
                context,
                new StaticOptionsMonitor<DlnaOptions>(new DlnaOptions()),
                new FixedFolderVisibility(hiddenFromListings: [], hiddenFromDelivery: []));
            var folders = await directories.AddRangeAsync(
                directories:
                [
                    new MediaDirectoryCreateDto
                    {
                        FullPath = _films,
                        Name = "Films",
                        Depth = 1,
                        IsSourceRoot = true,
                    },
                ],
                cancellationToken: CancellationToken.None);

            var files = CreateFileRepository(context);
            var fullPath = Path.Combine(_films, "film.mkv");
            var stored = await files.AddRangeAsync(
                files:
                [
                    new MediaFileCreateDto
                    {
                        FullPath = fullPath,
                        FileName = "film.mkv",
                        Title = "film",
                        Extension = ".mkv",
                        Mime = DlnaMime.VideoXMatroska,
                        UpnpClass = DlnaItemClass.VideoItem,
                        SizeInBytes = 1024,
                        FileCreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        FileModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        ContentStamp = "1024:638000000000000000",
                        DirectoryPublicId = folders[0].PublicId,
                    },
                ],
                cancellationToken: CancellationToken.None);

            return stored[0];
        }

        private static MediaFileRepository CreateFileRepository(DlnaDbContext context)
        {
            return new MediaFileRepository(
                context,
                new StaticOptionsMonitor<DlnaOptions>(new DlnaOptions()),
                new FixedFolderVisibility(hiddenFromListings: [], hiddenFromDelivery: []));
        }

        private static async Task AddLinkAsync(
            DlnaDbContext context,
            MediaFileDto film,
            string relativePath,
            SubtitleSource source)
        {
            var mediaFileId = await context.Files
                .Where(f => f.PublicId == film.PublicId)
                .Select(static f => f.Id)
                .SingleAsync(CancellationToken.None);

            _ = context.SubtitleFiles.Add(new SubtitleFileEntity
            {
                MediaFileId = mediaFileId,
                RelativePath = relativePath,
                Source = source,
            });
            _ = await context.SaveChangesAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
        }

        private static async Task<IReadOnlyList<SubtitleFileDto>> LinksOfAsync(
            SubtitleRepository subtitles,
            MediaFileDto film)
        {
            var links = await subtitles.GetForFilesAsync(
                mediaFilePublicIds: [film.PublicId],
                cancellationToken: CancellationToken.None);

            return links.TryGetValue(film.PublicId, out var found)
                ? found
                : [];
        }
    }
}
