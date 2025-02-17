using DLNAServer.Configuration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Collections.Concurrent;
using System.Data.Common;

namespace DLNAServer.Database
{
    public class SQLitePragmaInterceptor : DbConnectionInterceptor
    {
        private readonly ILogger<SQLitePragmaInterceptor> _logger;
        private readonly ServerConfig _serverConfig;
        public SQLitePragmaInterceptor(
            ILogger<SQLitePragmaInterceptor> logger,
            ServerConfig serverConfig)
        {
            _logger = logger;
            _serverConfig = serverConfig;
        }
        private static readonly ConcurrentDictionary<Guid, byte> _lastUsedConnectionId_ConnectionOpened = [];
        #region Opened
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            try
            {
                if (_lastUsedConnectionId_ConnectionOpened.TryAdd(eventData.ConnectionId, 0))
                {
                    using (var transaction = connection.BeginTransaction())
                    using (var command = connection.CreateCommand())
                    {
                        OpenedCommands(connection, command);

                        _ = command.ExecuteNonQuery();

                        transaction.Commit();
                    };
                }
            }
            catch { }

            base.ConnectionOpened(connection, eventData);
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_lastUsedConnectionId_ConnectionOpened.TryAdd(eventData.ConnectionId, 0))
                {
                    using (var transaction = connection.BeginTransaction())
                    await using (var command = connection.CreateCommand())
                    {
                        OpenedCommands(connection, command);

                        _ = await command.ExecuteNonQueryAsync(cancellationToken);

                        await transaction.CommitAsync(cancellationToken);
                    };
                }
            }
            catch { }

            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }

        private void OpenedCommands(DbConnection connection, DbCommand command)
        {
            command.CommandText = string.Empty;

            // reduces fsync() calls on disk writes, making transactions faster
            // trade durability for speed
            command.CommandText += "PRAGMA synchronous=NORMAL; ";

            // Changes the journaling mode to WAL, improves performance for concurrent reads/writes
            // WAL = Write-Ahead Logging
            command.CommandText += "PRAGMA journal_mode=WAL; ";

            // memory-map the database file in bytes, reducing disk I/O 
            //   If PRAGMA mmap_size is smaller than the SQLite database file size,
            //   SQLite will only memory-map the specified portion of the file,
            //   while the rest will still be accessed using traditional disk I/O.
            if (_serverConfig.ServerDatabaseMemoryMapLimitInMBytes > 0)
            {
                long databaseSize = new FileInfo(connection.Database).Length;
                long mmapSize = Math.Min(databaseSize, _serverConfig.ServerDatabaseMemoryMapLimitInMBytes * 1024 * 1024);
                command.CommandText += $"PRAGMA mmap_size={mmapSize}; ";
            }

            // database memory cache size 
            if (_serverConfig.ServerDatabaseCacheLimitInMBytes > 0)
            {
                // negative value set the cache size in KB instead of page count
                command.CommandText += $"PRAGMA cache_size=-{_serverConfig.ServerDatabaseCacheLimitInMBytes * 1024}; ";
            }
            else
            {
                // cache ~16MB in memory (4096 pages * 4kB per page) → check with DlnaDbContext.OptimizeDatabase for page_size
                command.CommandText += "PRAGMA cache_size=4096; ";
            }

            // set temporary tables and indices in RAM instead of disk 
            command.CommandText += "PRAGMA temp_store=MEMORY; ";
            //command.CommandText += "PRAGMA temp_store=2; ";

            //// Optimize Query Execution (indexes and query plans)
            command.CommandText += "PRAGMA analysis_limit=800; ";

            // automatically create temporary indexes to speed up queries.
            command.CommandText += "PRAGMA automatic_index=true; ";
        }
        #endregion Opened

        #region Closing 
        public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            try
            {
                // execute only after some executions
                if (ShouldFreeDBSpace())
                {
                    using (var transaction = connection.BeginTransaction())
                    using (var command = connection.CreateCommand())
                    {
                        ClosingCommands(command);

                        _ = command.ExecuteNonQuery();

                        transaction.Commit();
                    };
                }
            }
            catch { }

            return base.ConnectionClosing(connection, eventData, result);
        }

        public override async ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            try
            {
                // execute only after some executions
                if (ShouldFreeDBSpace())
                {
                    using (var transaction = connection.BeginTransaction())
                    await using (var command = connection.CreateCommand())
                    {
                        ClosingCommands(command);

                        _ = await command.ExecuteNonQueryAsync();

                        await transaction.CommitAsync();
                    };
                }
            }
            catch { }

            return await base.ConnectionClosingAsync(connection, eventData, result);
        }
        private static void ClosingCommands(DbCommand command)
        {
            command.CommandText = string.Empty;

            // QUERY OPTIMIZATIONS
            // runs internal optimizations like re-indexing and clearing unused pages
            command.CommandText += "PRAGMA optimize; ";

            // FREE UNUSED SPACE
            // rebuilds the database file, removing fragmentation and reducing its size
            // !!! locks the database while running command !!!
            command.CommandText += "VACUUM; ";
        }
        private static int _connectionCounter = 0;
        private const int _freeAfterConnection = short.MaxValue;
        private static bool ShouldFreeDBSpace()
        {
            return Interlocked.Increment(ref _connectionCounter) >= _freeAfterConnection
                && Interlocked.Exchange(ref _connectionCounter, 0) == _freeAfterConnection;
        }
        #endregion Closing 
        #region Disposing
        public override InterceptionResult ConnectionDisposing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            _ = _lastUsedConnectionId_ConnectionOpened.TryRemove(eventData.ConnectionId, out _);

            return base.ConnectionDisposing(connection, eventData, result);
        }
        public override async ValueTask<InterceptionResult> ConnectionDisposingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            _ = _lastUsedConnectionId_ConnectionOpened.TryRemove(eventData.ConnectionId, out _);

            return await base.ConnectionDisposingAsync(connection, eventData, result);
        }
        #endregion Disposing 
    }
}
