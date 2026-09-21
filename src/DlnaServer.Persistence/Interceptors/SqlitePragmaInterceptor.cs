using System.Data.Common;
using System.Globalization;
using System.Text;
using DlnaServer.Core.Configuration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace DlnaServer.Persistence.Interceptors
{
    /// <summary>
    /// Applies SQLite pragmas to each newly opened connection.
    /// </summary>
    /// <remarks>
    /// Deliberately does no maintenance on connection close. The reference ran <c>VACUUM</c> every 255
    /// closes, inside a transaction - and VACUUM takes an exclusive lock and rewrites the entire file,
    /// stalling any in-flight stream. Maintenance is an explicit, scheduled operation instead.
    /// </remarks>
    internal sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
    {
        private readonly IOptionsMonitor<DlnaOptions> _options;

        public SqlitePragmaInterceptor(IOptionsMonitor<DlnaOptions> options)
        {
            _options = options;
        }

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            ArgumentNullException.ThrowIfNull(connection);

            base.ConnectionOpened(connection, eventData);
            ApplyPragmas(connection);
        }

        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(connection);

            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = BuildPragmaScript(_options.CurrentValue.Database);
            _ = await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private void ApplyPragmas(DbConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = BuildPragmaScript(_options.CurrentValue.Database);
            _ = command.ExecuteNonQuery();
        }

        private static string BuildPragmaScript(DatabaseOptions database)
        {
            var script = new StringBuilder(256);

            // Write-ahead logging lets a reader and a writer proceed at the same time, which matters
            // because indexing writes while a renderer is browsing.
            _ = script.Append("PRAGMA journal_mode=WAL; ");
            _ = script.Append("PRAGMA synchronous=NORMAL; ");
            _ = script.Append("PRAGMA foreign_keys=ON; ");

            // One writer at a time, six background services and every request thread. Without this a
            // collision surfaces immediately as SQLITE_BUSY, which aborts a whole indexing batch.
            if (database.BusyTimeoutInMilliseconds > 0)
            {
                _ = script.Append(
                    CultureInfo.InvariantCulture,
                    $"PRAGMA busy_timeout={database.BusyTimeoutInMilliseconds}; ");
            }

            if (database.MemoryMapLimitInMegabytes > 0)
            {
                var bytes = (long)database.MemoryMapLimitInMegabytes * 1024 * 1024;
                _ = script.Append(CultureInfo.InvariantCulture, $"PRAGMA mmap_size={bytes}; ");
            }

            // A real else. The reference's equivalent block was preceded by a "//else" comment but was
            // an unconditional statement, so the configured value was always overwritten.
            if (database.CacheSizeInMegabytes > 0)
            {
                var kibibytes = database.CacheSizeInMegabytes * 1024;
                _ = script.Append(CultureInfo.InvariantCulture, $"PRAGMA cache_size=-{kibibytes}; ");
            }

            if (database.UseMemoryTempStore)
            {
                _ = script.Append("PRAGMA temp_store=MEMORY; ");
            }

            _ = script.Append("PRAGMA analysis_limit=800; ");

            return script.ToString();
        }
    }
}
