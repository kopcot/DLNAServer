using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DlnaServer.Persistence
{
    /// <inheritdoc cref="IIndexMaintenance"/>
    internal sealed class IndexMaintenance : IIndexMaintenance
    {
        private readonly DlnaDbContext _dbContext;

        public IndexMaintenance(DlnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IndexClearResult> ClearIndexAsync(CancellationToken cancellationToken = default)
        {
            // Counted before deleting rather than taken from the delete itself. SQLite implements a
            // foreign-key cascade as a trigger, and trigger-deleted rows do not reach changes() - so
            // DELETE FROM Directories over a parent and its child reports 1, and the operator is told
            // half the truth about what just happened.
            var files = await _dbContext.Files.CountAsync(cancellationToken);
            var directories = await _dbContext.Directories.CountAsync(cancellationToken);

            // Files before directories, and explicitly rather than by cascade: a file's directory is
            // nullable, so deleting the directories alone would leave any unparented row behind. Streams,
            // thumbnails and thumbnail content all cascade from the file.
            _ = await _dbContext.Files.ExecuteDeleteAsync(cancellationToken);
            _ = await _dbContext.Directories.ExecuteDeleteAsync(cancellationToken);

            // ExecuteDelete bypasses the change tracker, so anything this context still tracks now
            // describes rows that no longer exist.
            _dbContext.ForgetTrackedEntities();

            return new IndexClearResult(files, directories, await TryVacuumAsync(cancellationToken));
        }

        public async Task OptimizeAsync(CancellationToken cancellationToken = default)
        {
            // Not wrapped in a try like VACUUM is: optimize takes no exclusive lock, so a concurrent
            // reader is not a reason for it to fail, and a failure here is worth seeing rather than
            // absorbing. The caller decides whether a maintenance step is fatal to its own pass.
            _ = await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA optimize;", cancellationToken);
        }

        public async Task ReleaseMemoryAsync(CancellationToken cancellationToken = default)
        {
            // This context's own connection is checked out, not pooled, so ClearAllPools cannot reach
            // it. shrink_memory is what releases the page cache SQLite holds on the live one.
            _ = await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA shrink_memory;", cancellationToken);

            // Closes every idle pooled connection, which is the only way its page cache is freed - the
            // pool releases nothing when a connection is returned to it.
            SqliteConnection.ClearAllPools();
        }

        /// <remarks>
        /// Deleting rows only frees pages inside the file; without this a library that has been cleared
        /// keeps its full size on a NAS. VACUUM needs the database to itself, and this server is still
        /// serving - so a refusal is an expected outcome and never a reason to report the clear as
        /// failed. It cannot run inside a transaction either, which is why it follows the deletes rather
        /// than joining them.
        /// </remarks>
        private async Task<bool> TryVacuumAsync(CancellationToken cancellationToken)
        {
            try
            {
                _ = await _dbContext.Database.ExecuteSqlRawAsync("VACUUM;", cancellationToken);

                return true;
            }
            catch (SqliteException)
            {
                return false;
            }
        }
    }
}
