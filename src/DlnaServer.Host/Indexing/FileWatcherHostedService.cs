using DlnaServer.Core.Configuration;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Watching;
using DlnaServer.Core.Hosting;
using DlnaServer.Media.Watching;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Indexing
{
    /// <summary>
    /// Applies filesystem changes to the index, once each path has settled.
    /// </summary>
    /// <remarks>
    /// <b>Why the delay exists.</b> A file arriving over a slow network share becomes visible long before
    /// it is complete. Acting on the first event would index a partial file: wrong size, a content stamp
    /// that is stale the moment it is written, and metadata probed from a truncated stream. The reference
    /// server waits 30 seconds for exactly this reason, and that value is kept here as the default
    /// (<c>Dlna.Library.FileSettleSeconds</c>).
    /// <para>
    /// <b>What is different.</b> The wait is per path and restarts on every further write, so a slow
    /// upload defers indexing until it genuinely stops rather than for a fixed period after it started.
    /// The reference instead did <c>if (elapsed &lt; 30s) await Task.Delay(30s)</c> - a further full 30
    /// seconds regardless of how much had already passed - on a single-threaded reader, so it processed
    /// at most one event every 30 seconds and a bulk copy could back up for hours.
    /// </para>
    /// <para>
    /// Its de-duplication was also wrong: it compared <c>lastEventTime - DateTime.UtcNow</c> against
    /// 100 ms, which is always negative and therefore always true, so once a path had been seen every
    /// later identical event was discarded permanently - including the successive writes of a copy.
    /// Here events are coalesced per path with the newest winning.
    /// </para>
    /// </remarks>
    internal sealed partial class FileWatcherHostedService : BackgroundService
    {
        /// <summary>
        /// How often the pending set is examined.
        /// </summary>
        private static readonly TimeSpan _tickInterval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Quiet period after the last settled path before a scan runs.
        /// </summary>
        /// <remarks>
        /// A scan used to run on every tick that had at least one settled path. One pass costs two full
        /// recursive volume walks, ~25,504 stats during enumeration and ~25,504 more during reconcile,
        /// 1,055 directory probes and ~2,110 queries - and a bulk copy settles files at staggered times,
        /// so that was paid once per tick for as long as the copy lasted. On a 2 GB box the OS cannot
        /// keep 52,000 dentries plus the page cache plus the managed heap resident, so it thrashes rather
        /// than warming, working directly against the acoustic goal the byte cache exists to serve.
        /// <para>
        /// Waiting for the settling to go quiet collapses an import into a single pass. It is deliberately
        /// longer than <see cref="_tickInterval"/> so a batch spanning several ticks coalesces.
        /// </para>
        /// </remarks>
        private static readonly TimeSpan _coalesceQuietPeriod = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Longest a settled change waits for the quiet period before a scan runs anyway.
        /// </summary>
        /// <remarks>
        /// A copy that keeps settling files resets the quiet period every time, so without this ceiling a
        /// long import would leave nothing indexed until it finished. This bounds how stale the library
        /// can get while changes are still arriving; the settle window itself is what protects against
        /// indexing a partial file, and it is untouched.
        /// </remarks>
        private static readonly TimeSpan _maxCoalesceDelay = TimeSpan.FromMinutes(2);

        /// <summary>
        /// How long to leave a configured-but-unattached source folder before trying to watch it again.
        /// </summary>
        /// <remarks>
        /// The case this exists for is a share that was not mounted when the server started, which
        /// resolves itself minutes later without anyone touching the configuration. Rebuilding on every
        /// tick instead would tear every working watcher down twice a second for as long as the folder
        /// stayed missing, so the retry is deliberately slower than everything else in this loop.
        /// </remarks>
        private static readonly TimeSpan _unwatchedRetryInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Wait before rebuilding a watch that faulted again, doubling per consecutive fault up to
        /// <see cref="_maxFaultRestartDelay"/>.
        /// </summary>
        /// <remarks>
        /// An exhausted inotify limit faults every rebuilt watch at once, and each fault also asks for a
        /// resync - so rebuilding on every tick meant a full pass every two seconds, back to back.
        /// </remarks>
        private static readonly TimeSpan _firstFaultRestartDelay = TimeSpan.FromMinutes(1);

        private static readonly TimeSpan _maxFaultRestartDelay = TimeSpan.FromMinutes(30);

        /// <summary>
        /// How long a rebuilt watch has to stay fault-free before its next fault counts as a fresh one.
        /// </summary>
        /// <remarks>
        /// Longer than <see cref="_maxFaultRestartDelay"/>, so a fault that persists at the cap is never
        /// mistaken for a healthy watch and restarted at once.
        /// </remarks>
        private static readonly TimeSpan _faultHealthyPeriod = TimeSpan.FromHours(1);

        private readonly IFileSystemChangeWatcher _watcher;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly IDatabaseReadySignal _readySignal;
        private readonly ILogger<FileWatcherHostedService> _logger;

        /// <summary>
        /// Latest event per path. A path that keeps changing keeps resetting its own settle window.
        /// </summary>
        private readonly Dictionary<string, FileChangeEvent> _pending = new(StringComparer.Ordinal);

        /// <summary>
        /// How many settled changes are waiting for a scan to cover them.
        /// </summary>
        /// <remarks>
        /// Not reset until a scan actually succeeds. Clearing it earlier lost the change permanently on a
        /// transient failure: the watcher raises one event per filesystem change, and a change already
        /// observed does not happen again.
        /// <para>
        /// A count rather than the paths themselves, which is what this used to hold. The response to any
        /// settled change is always a <b>full</b> pass, so the paths were never read - and keeping one
        /// string per settled file meant a bulk import retained 25,000 of them, against a 60 MB heap
        /// target, to log a number.
        /// </para>
        /// </remarks>
        private int _awaitingCount;

        /// <summary>
        /// When the oldest un-scanned settled change appeared, and when the newest did.
        /// </summary>
        /// <remarks>
        /// Monotonic <see cref="TimeProvider.GetTimestamp"/> readings rather than wall-clock instants.
        /// Both windows below ask "how long since", and the wall clock cannot answer that across an NTP
        /// step - which a NAS without a battery-backed clock takes on every boot. A backward step held
        /// every pending path for the length of the step and also pinned
        /// <see cref="_maxCoalesceDelay"/> permanently true; a forward step settled everything at once
        /// and indexed files mid-copy.
        /// </remarks>
        private long? _awaitingSinceTimestamp;
        private long _lastSettledTimestamp;

        // The fault-restart backoff: a fault consumed from the watcher but not yet acted on, how many
        // fault restarts have happened back to back, and when the last one did.
        private bool _isFaultRestartPending;
        private int _consecutiveFaultRestarts;
        private long _lastFaultRestartTimestamp;

        public FileWatcherHostedService(
            IFileSystemChangeWatcher watcher,
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<DlnaOptions> options,
            TimeProvider timeProvider,
            IDatabaseReadySignal readySignal,
            ILogger<FileWatcherHostedService> logger)
        {
            _watcher = watcher;
            _scopeFactory = scopeFactory;
            _options = options;
            _timeProvider = timeProvider;
            _readySignal = readySignal;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Every settled change ends in a scan, and a scan needs a schema - see IDatabaseReadySignal.
            await _readySignal.WaitAsync(stoppingToken);

            WatchTargets watched;
            IReadOnlyList<string> attached;

            try
            {
                // Inside the guard, which it was not: reading the options can throw, and so can
                // normalising a malformed configured path - either one ended this service before the
                // loop it is guarded by had started.
                watched = ReadWatchTargets();
                attached = _watcher.Start(watched.SourceFolders, watched.ExcludeFolders);
            }
            catch (Exception exception)
            {
                // Starting a watch is the one failure this service cannot work around: on Linux an
                // exhausted inotify limit throws here, and without events there is nothing to react to.
                // Say so plainly and stop this service only - the server keeps serving what is indexed.
                LogWatcherStartFailed(exception);
                return;
            }

            var lastAttachTimestamp = _timeProvider.GetTimestamp();

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // Re-read every tick. SourceFolders and ExcludeFolders are hot-reloadable and
                        // neither carries a restart hint in the admin UI, so a folder added there has to
                        // move the watch with it - it used to be indexed by the next scan and never
                        // watched, while a removed one kept its inotify handle.
                        var current = ReadWatchTargets();
                        var faulted = TryTakeFaultRestart(_watcher.ConsumeRestartRequest());

                        // Comparing what ATTACHED against what was asked for. Comparing the request
                        // against the previous request - which is what this did - always matched, so a
                        // source folder absent when the watch was built stayed unwatched for the life of
                        // the process, and with UsePeriodicRescan shipping off nothing else would ever
                        // have noticed. Retried on an interval rather than every tick, because tearing
                        // every watcher down twice a second while a share stays unmounted is its own
                        // outage.
                        var isRetryDue = attached.Count < current.SourceFolders.Length
                            && _timeProvider.GetElapsedTime(lastAttachTimestamp) >= _unwatchedRetryInterval;

                        if (faulted || isRetryDue || !current.Matches(watched))
                        {
                            // Both assigned BEFORE the rebuild. A Restart that throws part-way used to
                            // leave them alone, so every later tick tore down whatever had attached and
                            // rebuilt it again - every two seconds, indefinitely, losing events in each
                            // gap, and reaching the generic tick-failed line rather than this one.
                            watched = current;
                            lastAttachTimestamp = _timeProvider.GetTimestamp();
                            attached = [];

                            attached = _watcher.Restart(current.SourceFolders, current.ExcludeFolders);
                            LogWatchRebuilt(faulted);
                        }

                        Drain(stoppingToken);
                        CollectSettled();

                        var scanned = await ScanIfDueAsync(stoppingToken);

                        // ConsumeResyncRequest is evaluated first and always clears the flag. A pass that
                        // has just succeeded already satisfies the resync, so running a second full pass
                        // in the same tick bought nothing and cost another two recursive volume walks.
                        // Left unconsumed while a fault restart is backing off: the dead watch keeps
                        // asking, and the rebuild that ends the wait is followed by a pass anyway.
                        if (!_isFaultRestartPending
                            && _watcher.ConsumeResyncRequest()
                            && !scanned
                            && !await RunFullScanAsync(stoppingToken))
                        {
                            // Consuming cleared the request, so a failed pass would otherwise drop it for
                            // good and leave the index wrong with no further signal. Same reasoning as the
                            // pending-event set in ScanIfDueAsync.
                            _watcher.RequestResync();
                        }
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        // The scan itself already handles its own failures; this covers draining the
                        // channel and reading options. One bad tick must not end the watch.
                        LogTickFailed(exception);
                    }

                    // Deliberately outside the guard above. It throws only on cancellation, which has to
                    // reach the shutdown handler below - and moving it inside would turn any other
                    // failure into a loop with no delay in it at all.
                    await Task.Delay(_tickInterval, _timeProvider, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        /// <summary>
        /// The folders to watch and the names to ignore, as configuration currently reads.
        /// </summary>
        /// <remarks>
        /// Normalised through <see cref="LibraryIndexer.NormaliseSourceFolders"/>, the same way the pass
        /// that reconciles the index normalises them. Handing the watcher the raw configured strings made
        /// the two disagree about what a source folder is, so a relative path or a trailing separator had
        /// the watch attached to one thing and the scan reconciling another.
        /// </remarks>
        private WatchTargets ReadWatchTargets()
        {
            var library = _options.CurrentValue.Library;

            return new WatchTargets(
                [.. LibraryIndexer.NormaliseSourceFolders(library.SourceFolders)],
                [.. library.ExcludeFolders]);
        }

        /// <summary>
        /// Whether a faulted watch should be rebuilt on this tick.
        /// </summary>
        /// <remarks>
        /// The first fault rebuilds at once; one that follows within <see cref="_faultHealthyPeriod"/>
        /// waits <see cref="ResolveFaultRestartDelay"/> after the previous rebuild. A fault consumed while
        /// waiting is remembered here, because consuming it cleared the watcher's own flag.
        /// </remarks>
        internal bool TryTakeFaultRestart(bool isFaultRaised)
        {
            _isFaultRestartPending |= isFaultRaised;

            if (!_isFaultRestartPending)
            {
                return false;
            }

            var sinceLast = _timeProvider.GetElapsedTime(_lastFaultRestartTimestamp);
            var isStreak = _consecutiveFaultRestarts > 0 && sinceLast < _faultHealthyPeriod;

            if (isStreak && sinceLast < ResolveFaultRestartDelay(_consecutiveFaultRestarts))
            {
                return false;
            }

            _consecutiveFaultRestarts = isStreak ? _consecutiveFaultRestarts + 1 : 1;
            _lastFaultRestartTimestamp = _timeProvider.GetTimestamp();
            _isFaultRestartPending = false;

            // Once per streak: the second fault in a row is the one that says the watch cannot stay up.
            if (_consecutiveFaultRestarts == 2)
            {
                LogWatchKeepsFaulting((int)_firstFaultRestartDelay.TotalMinutes, (int)_maxFaultRestartDelay.TotalMinutes);
            }

            return true;
        }

        /// <remarks>
        /// <c>internal</c> so the schedule is asserted directly, as with
        /// <c>MediaProcessingHostedService.ResolveIdleDelay</c>.
        /// </remarks>
        internal static TimeSpan ResolveFaultRestartDelay(int consecutiveFaultRestarts)
        {
            var doublings = Math.Clamp(consecutiveFaultRestarts - 1, 0, 16);
            var delay = _firstFaultRestartDelay * (1 << doublings);

            return delay > _maxFaultRestartDelay ? _maxFaultRestartDelay : delay;
        }

        /// <summary>
        /// Moves everything currently queued into the pending set, keeping only the latest per path.
        /// </summary>
        internal void Drain(CancellationToken cancellationToken)
        {
            while (_watcher.Events.TryRead(out var raised))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Last writer wins: the newest event carries the newest timestamp, which restarts the
                // settle window. That is what makes a long file copy resolve to a single index update.
                _pending[raised.FullPath] = raised;

                if (raised.OldFullPath is not null)
                {
                    // A rename is a removal at the old path and an arrival at the new one.
                    _pending[raised.OldFullPath] = raised with
                    {
                        FullPath = raised.OldFullPath,
                        OldFullPath = null,
                        Kind = FileChangeKind.Deleted,
                    };
                }
            }
        }

        /// <summary>
        /// Moves the paths quiet for the settle period into the set awaiting a scan.
        /// </summary>
        /// <remarks>
        /// Collecting and scanning are separate so several ticks' worth of settled paths can be covered by
        /// one pass - see <see cref="_coalesceQuietPeriod"/> for why that matters. This used to scan
        /// directly, once per tick that had anything settled.
        /// </remarks>
        internal void CollectSettled()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            var settlePeriod = TimeSpan.FromSeconds(_options.CurrentValue.Library.FileSettleSeconds);

            var settled = _pending
                .Where(entry => _timeProvider.GetElapsedTime(entry.Value.OccurredTimestamp) >= settlePeriod)
                .Select(static entry => entry.Key)
                .ToArray();

            if (settled.Length == 0)
            {
                return;
            }

            foreach (var path in settled)
            {
                _awaitingCount++;
                _ = _pending.Remove(path);
            }

            var now = _timeProvider.GetTimestamp();

            _awaitingSinceTimestamp ??= now;
            _lastSettledTimestamp = now;
        }

        /// <summary>
        /// Runs one pass once the settled changes have gone quiet, or have waited long enough.
        /// </summary>
        /// <remarks>
        /// A targeted incremental update is still the next refinement; a full pass is correct today and
        /// cannot leave the index inconsistent, which matters more than the extra work. What changed is
        /// how OFTEN it runs, not what it does.
        /// <para>
        /// The awaiting set is only cleared once a pass has actually succeeded. It used to be cleared
        /// before the scan, so a pass that failed on a transient error lost those events permanently -
        /// the files stayed unindexed until something unrelated touched the tree again.
        /// </para>
        /// </remarks>
        internal async Task<bool> ScanIfDueAsync(CancellationToken cancellationToken)
        {
            if (_awaitingSinceTimestamp is not { } awaitingSince)
            {
                return false;
            }

            var isQuiet = _timeProvider.GetElapsedTime(_lastSettledTimestamp) >= _coalesceQuietPeriod;
            var hasWaitedLongEnough = _timeProvider.GetElapsedTime(awaitingSince) >= _maxCoalesceDelay;

            if (!isQuiet && !hasWaitedLongEnough)
            {
                return false;
            }

            LogApplyingChanges(_awaitingCount);

            if (!await RunFullScanAsync(cancellationToken))
            {
                // Left awaiting on purpose, so the next tick retries. Nothing else would: the watcher
                // raises an event per filesystem change, and a change already observed does not happen
                // again.
                //
                // BOTH clocks are restarted, and only restarting _lastSettledUtc was a real defect: once
                // _maxCoalesceDelay had elapsed since the oldest settle, hasWaitedLongEnough was
                // permanently true and the guard above could never short-circuit again. Under a lasting
                // fault - a locked database during an admin rebuild, a full disc - a full pass then
                // relaunched on every tick, back to back, on a 4-core NAS. The comment here used to
                // claim the opposite.
                var now = _timeProvider.GetTimestamp();

                _lastSettledTimestamp = now;
                _awaitingSinceTimestamp = now;

                return false;
            }

            _awaitingCount = 0;
            _awaitingSinceTimestamp = null;

            return true;
        }

        /// <summary>
        /// Runs one indexing pass. Returns whether it completed, so the caller can keep its pending
        /// entries when it did not.
        /// </summary>
        private async Task<bool> RunFullScanAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var indexer = scope.ServiceProvider.GetRequiredService<ILibraryIndexer>();

                _ = await indexer.IndexAsync(cancellationToken);

                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // One failed pass must not stop the watcher; the next change triggers another.
                LogUpdateFailed(exception);

                return false;
            }
        }

        /// <summary>
        /// The folder set a watch was built over, so a later tick can tell whether it still matches.
        /// </summary>
        private sealed record WatchTargets(string[] SourceFolders, string[] ExcludeFolders)
        {
            public bool Matches(WatchTargets other)
            {
                // Ordinal and order-sensitive: these are paths, the target is Linux, and the list is
                // written back by the admin UI in the order the operator typed it. A record's own
                // equality would compare the array references, not their contents.
                return SourceFolders.AsSpan().SequenceEqual(other.SourceFolders)
                    && ExcludeFolders.AsSpan().SequenceEqual(other.ExcludeFolders);
            }
        }
    }
}
