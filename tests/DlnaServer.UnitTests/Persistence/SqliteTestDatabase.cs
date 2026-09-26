using DlnaServer.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DlnaServer.UnitTests.Persistence
{
    /// <summary>
    /// A throwaway SQLite database on disk, created by running the real migrations.
    /// </summary>
    /// <remarks>
    /// A file rather than <c>:memory:</c> on purpose: the in-memory provider does not enforce the
    /// collations, foreign keys and unique indexes these tests are checking.
    /// </remarks>
    internal sealed class SqliteTestDatabase : IDisposable
    {
        private readonly string _databasePath;

        public SqliteTestDatabase()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"dlna-test-{Guid.NewGuid():N}.sqlite");

            using var context = CreateContext();
            context.Database.Migrate();
        }

        public DlnaDbContext CreateContext(params IInterceptor[] interceptors)
        {
            var options = new DbContextOptionsBuilder<DlnaDbContext>()
                .UseSqlite($"Data Source={_databasePath}")
                .AddInterceptors(interceptors)
                .Options;

            return new DlnaDbContext(options);
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }
}
