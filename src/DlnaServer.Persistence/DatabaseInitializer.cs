using DlnaServer.Core.Files;
using DlnaServer.Core.Hosting;
using DlnaServer.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DlnaServer.Persistence
{
    /// <inheritdoc cref="IDatabaseInitializer"/>
    internal sealed class DatabaseInitializer : IDatabaseInitializer
    {
        /// <summary>
        /// SQLite reports a healthy database as exactly this from an integrity pragma.
        /// </summary>
        private const string IntegrityOk = "ok";

        // SQLITE_CORRUPT and SQLITE_NOTADB - the only two primary result codes that mean the file itself
        // is beyond use. Every other code describes a condition that may not be there a second later.
        private const int SqliteCorrupt = 11;
        private const int SqliteNotADatabase = 26;

        private readonly DlnaDbContext _dbContext;
        private readonly TimeProvider _timeProvider;
        private readonly IDatabaseResetSignal _resetSignal;

        public DatabaseInitializer(
            DlnaDbContext dbContext,
            TimeProvider timeProvider,
            IDatabaseResetSignal resetSignal)
        {
            _dbContext = dbContext;
            _timeProvider = timeProvider;
            _resetSignal = resetSignal;
        }

        public async Task<DatabaseInitializationResult> InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            var databasePath = GetDatabasePath();
            var fileExists = databasePath is not null && File.Exists(databasePath);

            // Before the health checks, because a reset does not care whether the file was healthy - and
            // this is the one moment when deleting it is safe. The host that raised the request has been
            // disposed, and nothing in this one has opened the database yet.
            if (_resetSignal.IsResetRequested)
            {
                if (fileExists)
                {
                    DeleteDatabaseFiles(databasePath!);
                }

                // Cleared only once the file is gone: a throw above leaves the request standing, so the
                // next start tries again rather than silently opening the database it was told to discard.
                _resetSignal.Reset();

                var reset = await ApplyMigrationsAsync(cancellationToken);

                return new DatabaseInitializationResult(
                    DatabaseInitializationOutcome.Reset,
                    reset,
                    CorruptFileBackupPath: null);
            }

            if (!fileExists)
            {
                var created = await ApplyMigrationsAsync(cancellationToken);

                return new DatabaseInitializationResult(
                    DatabaseInitializationOutcome.Created,
                    created,
                    CorruptFileBackupPath: null);
            }

            if (IsUsable(databasePath!))
            {
                var applied = await TryApplyMigrationsAsync(cancellationToken);

                if (applied is int migrationCount)
                {
                    return new DatabaseInitializationResult(
                        DatabaseInitializationOutcome.Opened,
                        migrationCount,
                        CorruptFileBackupPath: null);
                }
            }

            // Either the integrity check failed, the file could not be opened, or migrating it threw.
            // In every case the file is unusable, so move it aside and start clean.
            var backupPath = MoveAsideCorruptDatabase(databasePath!);
            var rebuilt = await ApplyMigrationsAsync(cancellationToken);

            return new DatabaseInitializationResult(
                DatabaseInitializationOutcome.Recreated,
                rebuilt,
                backupPath);
        }

        public async Task<string?> GetLastMachineNameAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.ServerInstances
                .AsNoTracking()
                .OrderByDescending(static s => s.LastStartedUtc)
                .Select(static s => s.MachineName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task RecordStartupAsync(string machineName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(machineName);

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

            var existing = await _dbContext.ServerInstances
                .FirstOrDefaultAsync(s => s.MachineName == machineName, cancellationToken);

            if (existing is null)
            {
                _ = await _dbContext.ServerInstances.AddAsync(
                    new ServerInstanceEntity
                    {
                        MachineName = machineName,
                        LastStartedUtc = nowUtc,
                    },
                    cancellationToken);
            }
            else
            {
                existing.LastStartedUtc = nowUtc;
            }

            _ = await _dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Runs SQLite's own consistency check on a throwaway connection.
        /// </summary>
        /// <remarks>
        /// Deliberately does NOT go through the <see cref="DlnaDbContext"/>. The configured connection
        /// string enables pooling, so a connection EF has opened keeps a handle on the file and the
        /// later move fails with a sharing violation. This connection disables pooling and is disposed
        /// before anything tries to move the file.
        /// </remarks>
        private static bool IsUsable(string databasePath)
        {
            var probeConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,

                // ReadWrite, not ReadOnly. The pragma interceptor puts this database in WAL mode, and
                // SQLite cannot replay a hot -wal over a read-only connection - it fails the open. So
                // after any unclean shutdown, which is exactly when a -wal is left behind, a perfectly
                // recoverable database was being declared corrupt and rebuilt empty.
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
            }.ToString();

            try
            {
                using var connection = new SqliteConnection(probeConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();

                // quick_check skips the index cross-checks that integrity_check performs, keeping startup
                // fast on a large library while still catching a damaged or non-database file.
                command.CommandText = "PRAGMA quick_check;";

                return string.Equals(
                    command.ExecuteScalar()?.ToString(),
                    IntegrityOk,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (SqliteException exception) when (IsCorruption(exception))
            {
                return false;
            }
        }

        /// <summary>
        /// Whether a SQLite failure means the file itself is damaged, rather than momentarily unavailable.
        /// </summary>
        /// <remarks>
        /// The distinction decides whether the index survives. Every failure used to be read as
        /// corruption, but a QNAP backup job holding a lock past the busy timeout raises
        /// <c>SQLITE_BUSY</c>, and a drive still spinning up raises <c>SQLITE_IOERR</c> - both from the
        /// same exception type as a genuinely damaged file. Treating those as corruption moved a healthy
        /// database aside and rebuilt it empty. Anything not listed here now propagates, so startup fails
        /// loudly instead of quietly discarding 25,504 rows.
        /// </remarks>
        private static bool IsCorruption(SqliteException exception)
        {
            return exception.SqliteErrorCode is SqliteCorrupt or SqliteNotADatabase;
        }

        /// <summary>
        /// Returns the number of migrations applied, or null when migrating failed because the
        /// database is damaged.
        /// </summary>
        private async Task<int?> TryApplyMigrationsAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await ApplyMigrationsAsync(cancellationToken);
            }
            catch (SqliteException exception) when (IsCorruption(exception))
            {
                return null;
            }
        }

        private async Task<int> ApplyMigrationsAsync(CancellationToken cancellationToken)
        {
            var pending = await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            var pendingCount = pending.Count();

            if (pendingCount > 0)
            {
                await _dbContext.Database.MigrateAsync(cancellationToken);
            }

            return pendingCount;
        }

        /// <summary>
        /// Moves the unusable database and its write-ahead files out of the way, returning the new path.
        /// </summary>
        private string MoveAsideCorruptDatabase(string databasePath)
        {
            // A failed migration can leave EF holding an open pooled connection to the damaged file.
            _dbContext.Database.CloseConnection();
            SqliteConnection.ClearAllPools();

            var backupPath = BackupFilePath.CreateUnique(databasePath, _timeProvider.GetUtcNow().UtcDateTime);

            File.Move(databasePath, backupPath, overwrite: false);

            foreach (var suffix in (string[])["-wal", "-shm"])
            {
                var sidecar = databasePath + suffix;

                if (File.Exists(sidecar))
                {
                    File.Move(sidecar, backupPath + suffix, overwrite: true);
                }
            }

            return backupPath;
        }

        /// <summary>
        /// Removes the database and its write-ahead sidecars.
        /// </summary>
        /// <remarks>
        /// The sidecars are the part that is easy to miss. A <c>-wal</c> left beside a deleted database is
        /// replayed into the empty file SQLite then creates, so the rows an operator asked to be rid of
        /// come back - and a <c>-shm</c> from a different file is a corruption report waiting to happen.
        /// <para>
        /// Pools are cleared first for the same reason as in <see cref="MoveAsideCorruptDatabase"/>:
        /// <c>Pooling=True</c> means a connection can outlive the context that opened it, and on Windows
        /// an open handle makes the delete fail outright rather than quietly.
        /// </para>
        /// </remarks>
        private void DeleteDatabaseFiles(string databasePath)
        {
            _dbContext.Database.CloseConnection();
            SqliteConnection.ClearAllPools();

            File.Delete(databasePath);

            foreach (var suffix in (string[])["-wal", "-shm"])
            {
                var sidecar = databasePath + suffix;

                if (File.Exists(sidecar))
                {
                    File.Delete(sidecar);
                }
            }
        }

        private string? GetDatabasePath()
        {
            var connectionString = _dbContext.Database.GetConnectionString();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return null;
            }

            var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;

            return string.IsNullOrWhiteSpace(dataSource) || dataSource.StartsWith(":memory:", StringComparison.Ordinal)
                ? null
                : Path.GetFullPath(dataSource);
        }
    }
}
