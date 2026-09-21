using DlnaServer.Core.Configuration;
using DlnaServer.Core.Hosting;
using DlnaServer.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Indexing
{
    /// <summary>
    /// Runs one indexing pass shortly after startup, and another whenever one is asked for.
    /// </summary>
    /// <remarks>
    /// Deliberately does not block startup. The reference ran its scan inside <c>StartAsync</c>, so the
    /// server did not begin listening until the whole library had been walked - on a large NAS library
    /// that is minutes during which no renderer can discover it.
    /// <para>
    /// The wait loop is what lets the admin UI rebuild the index without a restart. It is a loop rather
    /// than a fire-and-forget task per request because a pass takes seconds to minutes and holds a
    /// process-wide gate: running them here, one at a time, is the difference between a queue that drains
    /// and a pile of tasks all blocked on each other.
    /// </para>
    /// </remarks>
    internal sealed partial class LibraryIndexHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILibraryScanSignal _scanSignal;
        private readonly IDatabaseReadySignal _readySignal;
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly ILogger<LibraryIndexHostedService> _logger;

        public LibraryIndexHostedService(
            IServiceScopeFactory scopeFactory,
            ILibraryScanSignal scanSignal,
            IDatabaseReadySignal readySignal,
            IOptionsMonitor<DlnaOptions> options,
            ILogger<LibraryIndexHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _scanSignal = scanSignal;
            _readySignal = readySignal;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // StartAsync only returns once this method suspends, and nothing below suspends until the
            // scan reaches its first query: IndexAsync takes an uncontended semaphore, which completes
            // synchronously, and then walks the whole library. So on a 25,000-file volume the host was
            // held here for minutes, and the SSDP services registered after this one had not started -
            // the server was unreachable to every renderer for the length of the walk. Kestrel is
            // already listening by then, which is why it looked healthy in a browser.
            await Task.Yield();

            // Waits for a schema. Registration order guarantees nothing - see IDatabaseReadySignal.
            await _readySignal.WaitAsync(stoppingToken);

            await RunPassAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await WaitForNextPassAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                await RunPassAsync(stoppingToken);
            }
        }

        /// <remarks>
        /// Read from <see cref="IOptionsMonitor{TOptions}"/> on every iteration rather than captured
        /// once, so turning the timer on or changing its interval takes effect from the next wait instead
        /// of needing a restart. The current wait is not interrupted, which is the one visible seam:
        /// shortening the interval applies after the pass that is already pending.
        /// </remarks>
        private async Task WaitForNextPassAsync(CancellationToken stoppingToken)
        {
            var library = _options.CurrentValue.Library;

            if (!library.UsePeriodicRescan)
            {
                await _scanSignal.WaitForRequestAsync(stoppingToken);

                return;
            }

            // The return value is deliberately ignored. Either somebody asked for a pass or the interval
            // elapsed, and both mean the same thing here - run one.
            _ = await _scanSignal.WaitForRequestAsync(
                TimeSpan.FromMinutes(library.RescanIntervalMinutes),
                stoppingToken);
        }

        /// <remarks>
        /// Every failure is contained here rather than at the loop, so one bad pass costs one pass. A
        /// throw that escaped would end the service, and with it every later request for a scan - the
        /// same silent-capability-loss shape as ffmpeg going missing.
        /// </remarks>
        private async Task RunPassAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var indexer = scope.ServiceProvider.GetRequiredService<ILibraryIndexer>();

                var result = await indexer.IndexAsync(stoppingToken);

                // Only when the pass moved something. PRAGMA analysis_limit is set on every connection
                // but nothing ever ran the ANALYZE it bounds, so sqlite_stat1 was empty and every query
                // plan came from default heuristics. Here rather than inside the indexer because this is
                // the pass that follows a cold start or a rebuild, which is when the statistics are
                // actually wrong - the watcher's own scans run far too often to pay for it.
                if (result.FilesAdded > 0 || result.FilesUpdated > 0
                    || result.FilesRemoved > 0 || result.DirectoriesRemoved > 0)
                {
                    await RefreshStatisticsAsync(scope.ServiceProvider, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown during a scan is normal.
            }
            catch (Exception exception)
            {
                // A failed scan must not take the server down: what is already indexed stays servable.
                LogScanFailed(exception);
            }
        }

        /// <remarks>
        /// Its own frame so a failure here cannot be reported as a failed scan. It caught
        /// <see cref="SqliteException"/> in the caller's generic arm and logged <c>LogScanFailed</c>,
        /// which is misleading - the scan had already completed and returned. And this runs OUTSIDE
        /// <c>ILibraryIndexLock</c>, deliberately: <c>PRAGMA optimize</c> takes no exclusive lock, so a
        /// reader is no reason to hold the index. The admin rebuild's <c>VACUUM</c> does need the file to
        /// itself, though, so the two can still collide - and that collision is what this catch is for.
        /// </remarks>
        private async Task RefreshStatisticsAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            try
            {
                var maintenance = services.GetRequiredService<IIndexMaintenance>();
                await maintenance.OptimizeAsync(cancellationToken);

                LogStatisticsRefreshed();
            }
            catch (SqliteException exception)
            {
                LogStatisticsRefreshFailed(exception);
            }
        }
    }
}
