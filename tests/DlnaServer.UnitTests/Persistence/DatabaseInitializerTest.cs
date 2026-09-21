using DlnaServer.Core.Hosting;
using DlnaServer.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DlnaServer.UnitTests.Persistence
{
    [TestFixture]
    internal sealed class DatabaseInitializerTest
    {
        private string _databasePath = null!;

        [SetUp]
        public void SetUp()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"dlna-init-{Guid.NewGuid():N}.sqlite");
        }

        [TearDown]
        public void TearDown()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var directory = Path.GetDirectoryName(_databasePath)!;
            var pattern = Path.GetFileName(_databasePath) + "*";

            foreach (var file in Directory.EnumerateFiles(directory, pattern))
            {
                File.Delete(file);
            }
        }

        [Test]
        public async Task InitializeAsync_WhenDatabaseFileIsMissing_CreatesItFromMigrations()
        {
            // Arrange
            await using var context = CreateContext();
            var initializer = new DatabaseInitializer(context, TimeProvider.System, new DatabaseResetSignal());

            // Act
            var result = await initializer.InitializeAsync(CancellationToken.None);

            // Assert
            result.Outcome.Should().Be(DatabaseInitializationOutcome.Created,
                "because a first run has no database file and must build one rather than fail to start");
            result.MigrationsApplied.Should().BeGreaterThan(0,
                "because building an empty database means applying every migration");
            File.Exists(_databasePath).Should().BeTrue("because the database file was just created");
        }

        [Test]
        public async Task InitializeAsync_WhenDatabaseIsHealthy_OpensItWithoutRecreating()
        {
            // Arrange
            await using (var seedContext = CreateContext())
            {
                _ = await new DatabaseInitializer(seedContext, TimeProvider.System, new DatabaseResetSignal())
                    .InitializeAsync(CancellationToken.None);
                await new DatabaseInitializer(seedContext, TimeProvider.System, new DatabaseResetSignal())
                    .RecordStartupAsync("SEED-MACHINE", CancellationToken.None);
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // Act
            await using var context = CreateContext();
            var result = await new DatabaseInitializer(context, TimeProvider.System, new DatabaseResetSignal())
                .InitializeAsync(CancellationToken.None);

            // Assert
            result.Outcome.Should().Be(DatabaseInitializationOutcome.Opened,
                "because a healthy database must be reused, not rebuilt - rebuilding would discard the index");
            result.CorruptFileBackupPath.Should().BeNull("because nothing was moved aside");

            var machineName = await new DatabaseInitializer(context, TimeProvider.System, new DatabaseResetSignal())
                .GetLastMachineNameAsync(CancellationToken.None);
            machineName.Should().Be("SEED-MACHINE", "because the existing data survived startup");
        }

        [Test]
        public async Task InitializeAsync_WhenFileIsNotADatabase_RebuildsItAndKeepsTheOriginal()
        {
            // Arrange
            await File.WriteAllTextAsync(
                _databasePath,
                "this is not a SQLite database at all",
                CancellationToken.None);

            await using var context = CreateContext();
            var initializer = new DatabaseInitializer(context, TimeProvider.System, new DatabaseResetSignal());

            // Act
            var result = await initializer.InitializeAsync(CancellationToken.None);

            // Assert
            result.Outcome.Should().Be(DatabaseInitializationOutcome.Recreated,
                "because a file that is not a database cannot be opened and must be replaced");
            result.CorruptFileBackupPath.Should().NotBeNull(
                "because the unusable file is kept for investigation rather than deleted");
            File.Exists(result.CorruptFileBackupPath!).Should().BeTrue(
                "because the damaged file was moved aside, not removed");

            // Querying through the model proves the rebuilt schema is real, not just that a file exists.
            var rowCount = await context.ServerInstances.CountAsync(CancellationToken.None);
            rowCount.Should().Be(0, "because the rebuilt database has the full schema and is empty");
        }

        /// <summary>
        /// The Maintenance page's heavy rebuild. The request is raised in one host and acted on by the
        /// next, which is the only moment nothing holds the file open.
        /// </summary>
        [Test]
        public async Task InitializeAsync_WhenAResetIsRequested_DeletesTheDatabaseAndRebuildsIt()
        {
            // Arrange - a healthy database with something in it, so a rebuild is observable.
            await using (var seedContext = CreateContext())
            {
                _ = await new DatabaseInitializer(seedContext, TimeProvider.System, new DatabaseResetSignal())
                    .InitializeAsync(CancellationToken.None);
                await new DatabaseInitializer(seedContext, TimeProvider.System, new DatabaseResetSignal())
                    .RecordStartupAsync("BEFORE-RESET", CancellationToken.None);
            }

            var signal = new DatabaseResetSignal();
            signal.RequestReset();

            // Act
            await using var context = CreateContext();
            var result = await new DatabaseInitializer(context, TimeProvider.System, signal)
                .InitializeAsync(CancellationToken.None);

            // Assert
            result.Outcome.Should().Be(DatabaseInitializationOutcome.Reset,
                "because the operator asked for a clean database, and that is not the same as the file "
                + "having been found broken");
            result.CorruptFileBackupPath.Should().BeNull(
                "because nothing was wrong with it - keeping a copy would defeat the point of reclaiming "
                + "the space");

            var machineName = await new DatabaseInitializer(context, TimeProvider.System, signal)
                .GetLastMachineNameAsync(CancellationToken.None);
            machineName.Should().BeNull(
                "because the previous database is gone, not merely emptied of index rows");
        }

        /// <summary>
        /// A reset must happen once. If the request outlived the startup that acted on it, every later
        /// restart would silently wipe the database again.
        /// </summary>
        [Test]
        public async Task InitializeAsync_AfterActingOnAReset_ClearsTheRequest()
        {
            // Arrange
            var signal = new DatabaseResetSignal();
            signal.RequestReset();

            // Act
            await using var context = CreateContext();
            _ = await new DatabaseInitializer(context, TimeProvider.System, signal)
                .InitializeAsync(CancellationToken.None);

            // Assert
            signal.IsResetRequested.Should().BeFalse(
                "because the next start must open the database it just built, not discard it");
        }

        /// <summary>
        /// The write-ahead sidecars are the part that is easy to miss: a <c>-wal</c> left beside a deleted
        /// database is replayed into the empty file SQLite then creates, so the rows the operator asked to
        /// be rid of come back.
        /// </summary>
        [Test]
        public async Task InitializeAsync_WhenAResetIsRequested_RemovesTheWriteAheadSidecars()
        {
            // Arrange
            await using (var seedContext = CreateContext())
            {
                _ = await new DatabaseInitializer(seedContext, TimeProvider.System, new DatabaseResetSignal())
                    .InitializeAsync(CancellationToken.None);
                await new DatabaseInitializer(seedContext, TimeProvider.System, new DatabaseResetSignal())
                    .RecordStartupAsync("BEFORE-RESET", CancellationToken.None);
            }

            // Release the seed context's pooled handle: with Pooling=True the connection outlives the
            // context, and on Windows that open handle blocks writing the sidecars at all.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            File.WriteAllBytes(_databasePath + "-wal", new byte[64]);
            File.WriteAllBytes(_databasePath + "-shm", new byte[64]);

            var signal = new DatabaseResetSignal();
            signal.RequestReset();

            // Act
            await using var context = CreateContext();
            _ = await new DatabaseInitializer(context, TimeProvider.System, signal)
                .InitializeAsync(CancellationToken.None);

            // Assert - the fresh database may have opened its own sidecars, so what matters is that the
            // stale ones did not survive as the 64 zero bytes they were.
            var wal = _databasePath + "-wal";
            (!File.Exists(wal) || new FileInfo(wal).Length != 64).Should().BeTrue(
                "because a stale write-ahead log is replayed into the new file and brings the old rows "
                + "back with it");
        }

        [Test]
        public async Task InitializeAsync_WhenDatabaseIsTruncated_RebuildsIt()
        {
            // Arrange
            await using (var seedContext = CreateContext())
            {
                _ = await new DatabaseInitializer(seedContext, TimeProvider.System, new DatabaseResetSignal())
                    .InitializeAsync(CancellationToken.None);
            }

            // Release the seed context's pooled handle so the test can write to the file at all.
            // The initializer under test still runs on a pooling-enabled connection, which is the
            // configuration that previously failed to move a damaged file aside.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            CorruptDatabaseFile();

            // Act
            await using var context = CreateContext();
            var result = await new DatabaseInitializer(context, TimeProvider.System, new DatabaseResetSignal())
                .InitializeAsync(CancellationToken.None);

            // Assert
            result.Outcome.Should().Be(DatabaseInitializationOutcome.Recreated,
                "because a corrupted page must not be left to surface later as a mid-browse failure");

            await new DatabaseInitializer(context, TimeProvider.System, new DatabaseResetSignal())
                .RecordStartupAsync("AFTER-REBUILD", CancellationToken.None);
            var machineName = await new DatabaseInitializer(context, TimeProvider.System, new DatabaseResetSignal())
                .GetLastMachineNameAsync(CancellationToken.None);
            machineName.Should().Be("AFTER-REBUILD",
                "because the rebuilt database is fully usable straight away");
        }

        /// <summary>
        /// Overwrites the interior of the file, leaving the SQLite header intact so the file still
        /// opens but fails its integrity check - the case a header-only check would miss.
        /// </summary>
        private void CorruptDatabaseFile()
        {
            using var stream = new FileStream(_databasePath, FileMode.Open, FileAccess.Write);
            stream.Seek(offset: 4096, SeekOrigin.Begin);

            var garbage = new byte[4096];
            Array.Fill(garbage, (byte)0xEE);
            stream.Write(garbage, offset: 0, count: garbage.Length);
        }

        /// <summary>
        /// Builds the context the way the host does, pooling included.
        /// </summary>
        /// <remarks>
        /// Pooling matters: a pooled connection holds a handle on the database file, which is what made
        /// an earlier implementation fail to move a corrupt file aside on the real server while passing
        /// here. The test connection string must match production or it cannot catch that class of bug.
        /// </remarks>
        private DlnaDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<DlnaDbContext>()
                .UseSqlite($"Data Source={_databasePath};Cache=Shared;Pooling=True;")
                .Options;

            return new DlnaDbContext(options);
        }
    }
}
