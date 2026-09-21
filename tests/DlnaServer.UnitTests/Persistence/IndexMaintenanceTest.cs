using DlnaServer.Core.Configuration;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Dlna;
using DlnaServer.Persistence;
using DlnaServer.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DlnaServer.UnitTests.Persistence
{
    /// <summary>
    /// Covers the Maintenance page's light rebuild: empty the index, keep the file and the schema.
    /// </summary>
    [TestFixture]
    internal sealed class IndexMaintenanceTest
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

        [Test]
        public async Task ClearIndexAsync_RemovesEveryFileAndDirectory()
        {
            // Arrange
            await using var context = _database.CreateContext();
            await SeedAsync(context);

            // Act
            var result = await new IndexMaintenance(context).ClearIndexAsync(CancellationToken.None);

            // Assert
            result.FilesRemoved.Should().Be(2, "because both indexed files are gone");
            result.DirectoriesRemoved.Should().Be(2, "because both indexed folders are gone");

            await using var verify = _database.CreateContext();
            (await verify.Files.CountAsync(CancellationToken.None)).Should().Be(0,
                "because a rebuild starts from nothing");
            (await verify.Directories.CountAsync(CancellationToken.None)).Should().Be(0,
                "because a rebuild starts from nothing");
        }

        /// <summary>
        /// A file's directory is nullable, so deleting the directories alone would strand any row that
        /// never got one - and a stranded file is invisible to every listing and impossible to rescan
        /// over, because its path is still taken by the unique index.
        /// </summary>
        [Test]
        public async Task ClearIndexAsync_RemovesAFileWithNoDirectory()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var files = new MediaFileRepository(context, Options(), Visibility());
            _ = await files.AddRangeAsync(
                files: [CreateFile("/media/orphan.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var result = await new IndexMaintenance(context).ClearIndexAsync(CancellationToken.None);

            // Assert
            result.FilesRemoved.Should().Be(1,
                "because a file with no parent directory is still an indexed file");
        }

        /// <summary>
        /// The schema has to survive: the point of the light rebuild is that the server keeps running and
        /// the next scan can insert straight away, with no migration and no restart.
        /// </summary>
        [Test]
        public async Task ClearIndexAsync_LeavesTheSchemaUsable()
        {
            // Arrange
            await using var context = _database.CreateContext();
            await SeedAsync(context);
            _ = await new IndexMaintenance(context).ClearIndexAsync(CancellationToken.None);

            // Act - the scan that follows a rebuild, in miniature.
            await using var rescan = _database.CreateContext();
            var directories = new MediaDirectoryRepository(rescan, Options(), Visibility());
            var stored = await directories.AddRangeAsync(
                directories: [CreateDirectory("/media", "media", depth: 1, isSourceRoot: true)],
                cancellationToken: CancellationToken.None);

            // Assert
            stored.Should().ContainSingle(
                "because clearing rows must not disturb the tables the next scan writes into");
        }

        /// <summary>
        /// <c>PRAGMA analysis_limit</c> was set on every connection and nothing ever ran the
        /// <c>ANALYZE</c> it bounds, so the planner had no statistics at all.
        /// </summary>
        /// <remarks>
        /// The observable effect is <c>sqlite_stat1</c> existing and holding a row: <c>PRAGMA optimize</c>
        /// creates that table the first time it decides an analysis is worthwhile, and skips tables it
        /// judges unchanged - so a populated index is what makes the assertion meaningful.
        /// </remarks>
        [Test]
        public async Task OptimizeAsync_PopulatesTheQueryPlannerStatistics()
        {
            // Arrange
            await using var context = _database.CreateContext();
            await SeedAsync(context);

            // Act
            await new IndexMaintenance(context).OptimizeAsync(CancellationToken.None);

            // Assert
            await using var verify = _database.CreateContext();
            var statisticsRows = await CountStatisticsRowsAsync(verify);

            statisticsRows.Should().BeGreaterThan(0,
                "because without an ANALYZE every query plan in the server is chosen on default heuristics");
        }

        [Test]
        public async Task OptimizeAsync_OnAnEmptyIndex_DoesNotThrow()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var maintenance = new IndexMaintenance(context);

            // Act
            var optimize = async () => await maintenance.OptimizeAsync(CancellationToken.None);

            // Assert
            await optimize.Should().NotThrowAsync(
                "because the indexer calls this straight after a pass, and a fresh deployment's first pass "
                + "runs against a database that has only just been created");
        }

        /// <summary>
        /// The risk in releasing memory is not that it frees too little - it is that closing every
        /// pooled connection leaves the context unable to reach the database afterwards.
        /// </summary>
        [Test]
        public async Task ReleaseMemoryAsync_LeavesTheIndexReadable()
        {
            // Arrange
            await using var context = _database.CreateContext();
            await SeedAsync(context);

            // Act
            await new IndexMaintenance(context).ReleaseMemoryAsync(CancellationToken.None);

            // Assert
            await using var verify = _database.CreateContext();
            (await verify.Files.CountAsync(CancellationToken.None)).Should().Be(2,
                "because clearing the connection pools must reopen on the next query, not lose the index");
        }

        [Test]
        public async Task ReleaseMemoryAsync_OnAnEmptyIndex_DoesNotThrow()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var maintenance = new IndexMaintenance(context);

            // Act
            var release = async () => await maintenance.ReleaseMemoryAsync(CancellationToken.None);

            // Assert
            await release.Should().NotThrowAsync(
                "because an operator can press Empty the memory at any time, including before a first scan");
        }

        private static async Task<long> CountStatisticsRowsAsync(DlnaDbContext context)
        {
            var connection = context.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(CancellationToken.None);
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'sqlite_stat1';";

            var tableExists = Convert.ToInt64(await command.ExecuteScalarAsync(CancellationToken.None));

            if (tableExists == 0)
            {
                return 0;
            }

            command.CommandText = "SELECT COUNT(*) FROM sqlite_stat1;";

            return Convert.ToInt64(await command.ExecuteScalarAsync(CancellationToken.None));
        }

        private static async Task SeedAsync(DlnaDbContext context)
        {
            var directories = new MediaDirectoryRepository(context, Options(), Visibility());
            var roots = await directories.AddRangeAsync(
                directories: [CreateDirectory("/media", "media", depth: 1, isSourceRoot: true)],
                cancellationToken: CancellationToken.None);
            var children = await directories.AddRangeAsync(
                directories:
                [
                    CreateDirectory("/media/movies", "movies", depth: 2, isSourceRoot: false, parent: roots[0].PublicId),
                ],
                cancellationToken: CancellationToken.None);

            var files = new MediaFileRepository(context, Options(), Visibility());
            _ = await files.AddRangeAsync(
                files:
                [
                    CreateFile("/media/movies/one.mkv") with { DirectoryPublicId = children[0].PublicId },
                    CreateFile("/media/movies/two.mkv") with { DirectoryPublicId = children[0].PublicId },
                ],
                cancellationToken: CancellationToken.None);
        }

        /// <remarks>
        /// Nothing here exercises hiding, so both lists are empty and the repositories behave as they do
        /// on a library with no excluded and no temporarily hidden folders.
        /// </remarks>
        private static FixedFolderVisibility Visibility()
        {
            return new FixedFolderVisibility(hiddenFromListings: [], hiddenFromDelivery: []);
        }

        private static StaticOptionsMonitor<DlnaOptions> Options()
        {
            var options = new DlnaOptions();
            options.Library.ExcludeFolders = [];

            return new StaticOptionsMonitor<DlnaOptions>(options);
        }

        private static MediaDirectoryCreateDto CreateDirectory(
            string fullPath,
            string name,
            int depth,
            bool isSourceRoot,
            Guid? parent = null)
        {
            return new MediaDirectoryCreateDto
            {
                FullPath = fullPath,
                Name = name,
                Depth = depth,
                IsSourceRoot = isSourceRoot,
                ParentDirectoryPublicId = parent,
            };
        }

        private static MediaFileCreateDto CreateFile(string fullPath)
        {
            var fileName = Path.GetFileName(fullPath);

            return new MediaFileCreateDto
            {
                FullPath = fullPath,
                FileName = fileName,
                Title = Path.GetFileNameWithoutExtension(fileName),
                Extension = Path.GetExtension(fileName).ToLowerInvariant(),
                Mime = DlnaMime.VideoXMatroska,
                UpnpClass = DlnaItemClass.VideoItem,
                SizeInBytes = 1024,
                FileCreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FileModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ContentStamp = "1024:638000000000000000",
            };
        }
    }
}
