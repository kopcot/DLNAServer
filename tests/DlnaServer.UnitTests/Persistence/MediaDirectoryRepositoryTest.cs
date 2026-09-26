using DlnaServer.Core.Contracts;
using DlnaServer.Core.Configuration;
using DlnaServer.Persistence;
using DlnaServer.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using static DlnaServer.UnitTests.Persistence.PersistenceTestData;

namespace DlnaServer.UnitTests.Persistence
{
    [TestFixture]
    internal sealed class MediaDirectoryRepositoryTest
    {
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

        /// <summary>
        /// The caller only ever holds a PublicId, so the repository has to resolve it to the internal
        /// integer key before it can store the foreign key.
        /// </summary>
        [Test]
        public async Task AddRangeAsync_WithParentPublicId_LinksChildToParent()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var roots = await repository.AddRangeAsync(
                directories: [CreateDirectory("/media", "media", depth: 1, isSourceRoot: true)],
                cancellationToken: CancellationToken.None);
            var rootPublicId = roots[0].PublicId;

            // Act
            var children = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/movies", "movies", depth: 2, isSourceRoot: false, parent: rootPublicId),
                ],
                cancellationToken: CancellationToken.None);

            // Assert
            children[0].ParentDirectoryPublicId.Should().Be(rootPublicId,
                "because the parent was supplied by public identifier and must round-trip as one");

            var storedParentKey = await context.Directories.AsNoTracking()
                .Where(d => d.FullPath == "/media/movies")
                .Select(static d => d.ParentDirectoryId)
                .FirstAsync(CancellationToken.None);
            storedParentKey.Should().NotBeNull(
                "because the foreign key stored in the database is the parent's integer key, not its GUID");
        }

        [Test]
        public async Task AddRangeAsync_WithUnknownParentPublicId_FailsLoudly()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            // Act
            var act = async () => await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/orphan", "orphan", depth: 2, isSourceRoot: false, parent: Guid.NewGuid()),
                ],
                cancellationToken: CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>(
                "because silently storing a null parent would detach the folder from the browse tree - "
                + "the reference threw ApplicationException from deep inside a parallel loop instead");
        }

        [Test]
        public async Task GetChildrenAsync_ReturnsOnlyDirectChildren_OrderedByName()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var roots = await repository.AddRangeAsync(
                directories: [CreateDirectory("/media", "media", depth: 1, isSourceRoot: true)],
                cancellationToken: CancellationToken.None);
            var rootPublicId = roots[0].PublicId;

            var children = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/zebra", "zebra", depth: 2, isSourceRoot: false, parent: rootPublicId),
                    CreateDirectory("/media/apple", "apple", depth: 2, isSourceRoot: false, parent: rootPublicId),
                ],
                cancellationToken: CancellationToken.None);

            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory(
                        "/media/apple/seeds",
                        "seeds",
                        depth: 3,
                        isSourceRoot: false,
                        parent: children.First(static d => d.Name == "apple").PublicId),
                ],
                cancellationToken: CancellationToken.None);

            await AddMediaAsync(context, "/media/apple/seeds", "/media/zebra");

            // Act
            var actual = await repository.GetChildrenAsync(
                rootPublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            actual.Select(static d => d.Name).Should().Equal(["apple", "zebra"],
                "because only direct children are listed, ordered by name for the container listing");
        }

        [Test]
        public async Task RemoveByPublicIdsAsync_ForParentDirectory_CascadesToSubdirectories()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var roots = await repository.AddRangeAsync(
                directories: [CreateDirectory("/media", "media", depth: 1, isSourceRoot: true)],
                cancellationToken: CancellationToken.None);
            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/movies", "movies", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                ],
                cancellationToken: CancellationToken.None);

            // Act
            await using var deleteContext = _database.CreateContext();
            _ = await CreateRepository(deleteContext).RemoveByPublicIdsAsync(
                publicIds: [roots[0].PublicId],
                cancellationToken: CancellationToken.None);

            // Assert
            await using var verifyContext = _database.CreateContext();
            var remaining = await verifyContext.Directories.CountAsync(CancellationToken.None);
            remaining.Should().Be(0,
                "because removing a directory takes its subdirectories with it by cascade");
        }

        [Test]
        public async Task GetSourceRootsAsync_ReturnsOnlyConfiguredRoots()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var roots = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media", "media", depth: 1, isSourceRoot: true),
                    CreateDirectory("/photos", "photos", depth: 1, isSourceRoot: true),
                ],
                cancellationToken: CancellationToken.None);
            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/movies", "movies", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                ],
                cancellationToken: CancellationToken.None);

            await AddMediaAsync(context, "/media/movies", "/photos");

            // Act
            var actual = await repository.GetSourceRootsAsync(CancellationToken.None);

            // Assert
            actual.Select(static d => d.Name).Should().Equal(["media", "photos"],
                "because the DLNA root lists configured source folders only, not every indexed folder");
        }

        /// <summary>
        /// The name filter must ignore capitals, which is what a search box is expected to do.
        /// </summary>
        [Test]
        public async Task SearchAsync_ByName_IgnoresCapitals()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/Films", "Films", depth: 2, isSourceRoot: false),
                    CreateDirectory("/media/Music", "Music", depth: 2, isSourceRoot: false),
                ],
                cancellationToken: CancellationToken.None);

            await AddMediaAsync(context, "/media/Films", "/media/Music");

            // Act
            var found = await repository.SearchAsync(
                new MediaDirectorySearchRequest { NameContains = "films" },
                CancellationToken.None);

            // Assert
            found.Should().ContainSingle("because one folder matches regardless of case")
                .Which.Name.Should().Be("Films",
                    "because the lower-case term must find the capitalised name");
        }

        /// <summary>
        /// A term containing LIKE wildcards must be searched for literally.
        /// </summary>
        /// <remarks>
        /// Folder names are full of underscores, and an unescaped <c>_</c> in a LIKE pattern matches any
        /// single character - so without escaping this search would return rows that do not contain the
        /// term at all, which is worse than returning none.
        /// </remarks>
        [Test]
        public async Task SearchAsync_ByNameContainingWildcards_TreatsThemAsLiteralText()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/a_b", "a_b", depth: 2, isSourceRoot: false),
                    CreateDirectory("/media/axb", "axb", depth: 2, isSourceRoot: false),
                ],
                cancellationToken: CancellationToken.None);

            await AddMediaAsync(context, "/media/a_b", "/media/axb");

            // Act
            var found = await repository.SearchAsync(
                new MediaDirectorySearchRequest { NameContains = "a_b" },
                CancellationToken.None);

            // Assert
            found.Should().ContainSingle("because only the literal underscore matches")
                .Which.Name.Should().Be("a_b",
                    "because an unescaped underscore would also have matched axb");
        }

        /// <summary>
        /// The path filter is what finds a folder by an ancestor rather than by its own name.
        /// </summary>
        [Test]
        public async Task SearchAsync_ByPath_FindsEveryFolderBeneathAnAncestor()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/Films/SciFi", "SciFi", depth: 3, isSourceRoot: false),
                    CreateDirectory("/media/Films/Drama", "Drama", depth: 3, isSourceRoot: false),
                    CreateDirectory("/media/Music/Rock", "Rock", depth: 3, isSourceRoot: false),
                ],
                cancellationToken: CancellationToken.None);

            await AddMediaAsync(
                context,
                "/media/Films/SciFi",
                "/media/Films/Drama",
                "/media/Music/Rock");

            // Act
            var found = await repository.SearchAsync(
                new MediaDirectorySearchRequest { PathContains = "/Films/" },
                CancellationToken.None);

            // Assert
            found.Select(static d => d.Name).Should().BeEquivalentTo(["Drama", "SciFi"],
                "because the ancestor's name must find every folder below it and nothing outside it");
        }

        [Test]
        public async Task SearchAsync_WithBothFilters_RequiresBothOfThem()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/Films/Rock", "Rock", depth: 3, isSourceRoot: false),
                    CreateDirectory("/media/Music/Rock", "Rock", depth: 3, isSourceRoot: false),
                ],
                cancellationToken: CancellationToken.None);

            await AddMediaAsync(context, "/media/Films/Rock", "/media/Music/Rock");

            // Act
            var found = await repository.SearchAsync(
                new MediaDirectorySearchRequest { NameContains = "rock", PathContains = "/Music/" },
                CancellationToken.None);

            // Assert
            found.Should().ContainSingle("because the filters combine with AND rather than OR")
                .Which.FullPath.Should().Be("/media/Music/Rock",
                    "because the folder matching only the name filter must be excluded");
        }

        /// <summary>
        /// An empty form must not try to return the whole index.
        /// </summary>
        [Test]
        public async Task SearchAsync_WithNoFilters_IsStillBoundedByTake()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/a", "a", depth: 2, isSourceRoot: false),
                    CreateDirectory("/media/b", "b", depth: 2, isSourceRoot: false),
                    CreateDirectory("/media/c", "c", depth: 2, isSourceRoot: false),
                ],
                cancellationToken: CancellationToken.None);

            await AddMediaAsync(context, "/media/a", "/media/b", "/media/c");

            // Act
            var found = await repository.SearchAsync(
                new MediaDirectorySearchRequest { Take = 2 },
                CancellationToken.None);

            // Assert
            found.Should().HaveCount(2,
                "because a search with no criteria must still be capped by Take");
        }

        [Test]
        public async Task GetChildrenAsync_OmitsExcludedDirectories()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context, "@Recycle");
            var roots = await repository.AddRangeAsync(
                directories: [CreateDirectory("/media", "media", depth: 1, isSourceRoot: true)],
                cancellationToken: CancellationToken.None);
            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/@Recycle", "@Recycle", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                    CreateDirectory("/media/movies", "movies", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                ],
                cancellationToken: CancellationToken.None);

            // Both hold media, so exclusion is the only thing that can be hiding one of them.
            await AddMediaAsync(context, "/media/@Recycle", "/media/movies");

            // Act
            var visible = await repository.GetChildrenAsync(
                roots[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            visible.Should().ContainSingle(d => d.Name == "movies",
                "because an excluded folder is hidden from a renderer even once it is already indexed");
        }

        /// <summary>
        /// The listing methods hide an excluded folder from everybody, so lookup by identifier is the only
        /// way back to one - and it has to keep working, or an operator cannot reach what they hid and a
        /// renderer mid-stream would be cut off.
        /// </summary>
        [Test]
        public async Task GetByPublicIdAsync_StillReturnsAnExcludedDirectory()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context, "@Recycle");
            var stored = await repository.AddRangeAsync(
                directories: [CreateDirectory("/media/@Recycle", "@Recycle", depth: 2, isSourceRoot: false)],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            found.Should().NotBeNull(
                "because hiding governs listings only - delivery and navigation by identifier are exempt");
        }

        /// <summary>
        /// QNAP's own <c>.streams</c> and <c>.@upload_cache</c> are indexed by a volume-wide scan and hold
        /// nothing playable, and they were being offered to a television as top-level containers.
        /// </summary>
        [Test]
        public async Task GetChildrenAsync_OmitsAFolderWithNoMediaBeneathIt()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var roots = await repository.AddRangeAsync(
                directories: [CreateDirectory("/media", "media", depth: 1, isSourceRoot: true)],
                cancellationToken: CancellationToken.None);
            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/.streams", ".streams", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                    CreateDirectory("/media/movies", "movies", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                ],
                cancellationToken: CancellationToken.None);

            await AddMediaAsync(context, "/media/movies");

            // Act
            var visible = await repository.GetChildrenAsync(
                roots[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            visible.Should().ContainSingle(d => d.Name == "movies",
                "because a container a renderer cannot reach any media through is not worth listing");
        }

        /// <summary>
        /// The test is the whole subtree, not the folder's own files - otherwise every folder that merely
        /// groups other folders would vanish and the tree would have no way in.
        /// </summary>
        [Test]
        public async Task GetChildrenAsync_KeepsAFolderWhoseMediaIsOnlyDeeperDown()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var roots = await repository.AddRangeAsync(
                directories: [CreateDirectory("/media", "media", depth: 1, isSourceRoot: true)],
                cancellationToken: CancellationToken.None);
            var films = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/Films", "Films", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                ],
                cancellationToken: CancellationToken.None);
            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/Films/2024/SciFi", "SciFi", depth: 4, isSourceRoot: false, parent: films[0].PublicId),
                ],
                cancellationToken: CancellationToken.None);

            await AddMediaAsync(context, "/media/Films/2024/SciFi");

            // Act
            var visible = await repository.GetChildrenAsync(
                roots[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            visible.Should().ContainSingle(d => d.Name == "Films",
                "because the only film in the library is three levels below it and still counts");
        }

        /// <summary>
        /// The reported case: the media under a folder becomes hidden, and its parent is left with nothing
        /// visible anywhere beneath it.
        /// </summary>
        [Test]
        public async Task GetChildrenAsync_OmitsAFolderWhoseOnlyMediaIsHidden()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context, "@Recycle");
            var roots = await repository.AddRangeAsync(
                directories: [CreateDirectory("/media", "media", depth: 1, isSourceRoot: true)],
                cancellationToken: CancellationToken.None);
            var films = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/Films", "Films", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                ],
                cancellationToken: CancellationToken.None);
            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/Films/@Recycle", "@Recycle", depth: 3, isSourceRoot: false, parent: films[0].PublicId),
                ],
                cancellationToken: CancellationToken.None);

            await AddMediaAsync(context, "/media/Films/@Recycle");

            // Act
            var visible = await repository.GetChildrenAsync(
                roots[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            visible.Should().BeEmpty(
                "because hiding the only folder holding media leaves the parent an empty container, and "
                + "it takes effect on the next request rather than waiting for a rescan");
        }

        /// <summary>
        /// The subtree test is a path range, and a sibling whose name merely starts with the same
        /// characters must not satisfy it.
        /// </summary>
        /// <remarks>
        /// <c>/media/Films2/film.mkv</c> sorts above <c>/media/Films/</c>, so a lower bound on its own
        /// would count it as content of <c>/media/Films</c> and keep an empty folder listed.
        /// </remarks>
        [Test]
        public async Task GetChildrenAsync_DoesNotCountMediaInASiblingWithALongerName()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var roots = await repository.AddRangeAsync(
                directories: [CreateDirectory("/media", "media", depth: 1, isSourceRoot: true)],
                cancellationToken: CancellationToken.None);
            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/Films", "Films", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                    CreateDirectory("/media/Films2", "Films2", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                ],
                cancellationToken: CancellationToken.None);

            await AddMediaAsync(context, "/media/Films2");

            // Act
            var visible = await repository.GetChildrenAsync(
                roots[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            visible.Should().ContainSingle(d => d.Name == "Films2",
                "because only Films2 holds a file, and Films must not borrow it through a prefix match");
        }

        /// <summary>
        /// Stored paths carry the separator of whichever host indexed them, so the subtree test has to
        /// recognise a Windows path while running on Linux and the other way round.
        /// </summary>
        /// <remarks>
        /// Reading <see cref="Path.DirectorySeparatorChar"/> instead would make this pass on Windows and
        /// hide the entire library on the NAS, silently - the same trap the recursive clear and recreate
        /// operations hit.
        /// </remarks>
        [Test]
        public async Task GetChildrenAsync_RecognisesMediaUnderAWindowsStylePath()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var roots = await repository.AddRangeAsync(
                directories: [CreateDirectory(@"C:\media", "media", depth: 1, isSourceRoot: true)],
                cancellationToken: CancellationToken.None);
            _ = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory(@"C:\media\Films", "Films", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                    CreateDirectory(@"C:\media\Empty", "Empty", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                ],
                cancellationToken: CancellationToken.None);

            await AddMediaAsync(context, @"C:\media\Films");

            // Act
            var visible = await repository.GetChildrenAsync(
                roots[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            visible.Should().ContainSingle(d => d.Name == "Films",
                "because a backslash-separated path is a real stored shape, not only a test artefact");
        }

        /// <remarks>
        /// Paths only: the subtree test reads the file's stored path rather than its foreign key, so a
        /// file needs no directory row to make the folder above it visible.
        /// </remarks>
        private static async Task AddMediaAsync(DlnaDbContext context, params string[] directoryPaths)
        {
            var files = new MediaFileCreateDto[directoryPaths.Length];

            for (var index = 0; index < directoryPaths.Length; index++)
            {
                var separator = directoryPaths[index].Contains('\\', StringComparison.Ordinal) ? '\\' : '/';

                files[index] = CreateFile($"{directoryPaths[index]}{separator}film{index}.mkv");
            }

            var options = new DlnaOptions();
            options.Library.ExcludeFolders = [];

            var repository = new MediaFileRepository(
                context,
                new StaticOptionsMonitor<DlnaOptions>(options),
                new FixedFolderVisibility(hiddenFromListings: [], hiddenFromDelivery: []));

            _ = await repository.AddRangeAsync(files, CancellationToken.None);
        }

        /// <summary>
        /// The batched read the indexer replaced two per-path loops with.
        /// </summary>
        /// <remarks>
        /// It followed <c>GetExistingPathsAsync</c> with a <c>GetByPathAsync</c> per known path, in two
        /// separate places, at roughly 2,110 round trips per pass on the live library. Deliberately NOT
        /// filtered by <c>ExcludeFolders</c>: the indexer decides what to insert and what to relink from
        /// this, and hiding a row makes the next scan re-insert a path that already exists and violate
        /// the unique index - so the exclusion case below asserts the row is still returned.
        /// </remarks>
        [Test]
        public async Task GetExistingByPathAsync_ReturnsOnlyTheIndexedPathsWithTheirRows()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context, "private");
            var stored = await repository.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media", "media", depth: 1, isSourceRoot: true),
                    CreateDirectory("/media/Private", "Private", depth: 2, isSourceRoot: false),
                ],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.GetExistingByPathAsync(
                fullPaths: ["/media", "/media/Private", "/media/never-indexed"],
                cancellationToken: CancellationToken.None);

            // Assert
            found.Keys.Should().BeEquivalentTo(["/media", "/media/Private"],
                "because a path that is not indexed has no row to return, and an excluded one still does");
            var storedRoot = stored.Single(static d => d.FullPath == "/media");

            found["/media"].PublicId.Should().Be(storedRoot.PublicId,
                "because the caller uses these rows to key its own path-to-identifier map");
            found["/media"].IsSourceRoot.Should().BeTrue(
                "because the relink pass compares this against configuration and needs the stored value");
        }

        [Test]
        public async Task GetExistingByPathAsync_WithNoPaths_QueriesNothing()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            // Act
            var found = await repository.GetExistingByPathAsync(
                fullPaths: [],
                cancellationToken: CancellationToken.None);

            // Assert
            found.Should().BeEmpty(
                "because a scan that discovered no directories must not issue a query at all");
        }

        /// <remarks>
        /// Every listing hides excluded folders unconditionally, so a test that exercises hiding passes
        /// the names it wants hidden; every other test gets an empty list and is unaffected.
        /// </remarks>
        private static MediaDirectoryRepository CreateRepository(
            DlnaDbContext context,
            params string[] excludeFolders)
        {
            var options = new DlnaOptions();
            options.Library.ExcludeFolders = excludeFolders;

            return new MediaDirectoryRepository(
                context,
                new StaticOptionsMonitor<DlnaOptions>(options),
                new FixedFolderVisibility(hiddenFromListings: excludeFolders, hiddenFromDelivery: []));
        }

    }
}
