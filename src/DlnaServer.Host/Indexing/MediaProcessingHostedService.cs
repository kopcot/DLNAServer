using DlnaServer.Persistence;
using DlnaServer.Core.Diagnostics;
using System.Runtime;
using System.Diagnostics;
using System.Collections.Concurrent;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Delivery;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Hosting;
using DlnaServer.Media.Processing;
using DlnaServer.Persistence.Repositories;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Indexing
{
    /// <summary>
    /// Generates metadata and thumbnails for indexed files, in the background.
    /// </summary>
    /// <remarks>
    /// The single most important property of this service is that it is <b>not</b> on the Browse path.
    /// The reference generated metadata and thumbnails inside the ContentDirectory Browse call, so the
    /// first time a renderer opened a folder of unprocessed videos it waited on ffmpeg and SkiaSharp for
    /// every item before receiving any DIDL-Lite at all - and renderers time out.
    /// <para>
    /// <b>Work is done by a small, fixed number of workers - not one file at a time, and not one per
    /// core.</b> This ran strictly sequentially until 2026-09-06, on the reasoning that thumbnailing
    /// allocates natively through Skia and ffmpeg and that running several at once multiplies resident
    /// memory. The memory half of that is true and the throughput half was not: a first pass over 25,504
    /// files is the longest thing this server ever does and it used one core of four, because each file
    /// spends most of its time waiting on a disc read or an ffmpeg process rather than computing.
    /// </para>
    /// <para>
    /// The bargain is that the extra memory is <b>borrowed, not kept</b>. A burst is acceptable while
    /// previews are being made; what is not acceptable is the server sitting at that level afterwards.
    /// <see cref="SettleAsync"/> is what makes that true, and it runs on the transition from working to
    /// idle - see the note there on why it forces a collection when section 3 of <c>docs/decisions.md</c> forbids
    /// exactly that.
    /// </para>
    /// </remarks>
    internal sealed partial class MediaProcessingHostedService : BackgroundService
    {
        /// <summary>
        /// Files claimed per pass. Small: each one is read from disk, so a large claim would hold a lot
        /// of paths for work that takes seconds per item anyway.
        /// </summary>
        private const int BatchSize = 25;

        /// <summary>
        /// Ceiling on concurrent workers, whatever the machine reports.
        /// </summary>
        private const int MaxWorkers = 2;

        private const long BytesPerMegabyte = 1024L * 1024L;

        /// <summary>
        /// Attempts before a file is left alone. Without this a permanently unreadable file is retried
        /// on every pass forever.
        /// </summary>
        private const int MaxFailureCount = 3;

        private static readonly TimeSpan _firstRetryDelay = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan _laterRetryDelay = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Empty passes tolerated at <see cref="_idleDelay"/> before the wait starts doubling.
        /// </summary>
        /// <remarks>
        /// Four, so the tail of a real workload - a batch that happens to find nothing while the indexer
        /// is still inserting - is not treated as an idle library.
        /// </remarks>
        private const int EmptyPassesBeforeBackoff = 4;

        /// <summary>
        /// Cap on the shift exponent, so a long-idle server cannot overflow it.
        /// </summary>
        private const int MaxDoublings = 16;

        private static readonly TimeSpan _idleDelay = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan _busyDelay = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Ceiling for the idle wait once it has backed off.
        /// </summary>
        /// <remarks>
        /// A fixed 15 seconds meant a settled library ran <c>GetPendingProcessingAsync</c> 5,760 times a
        /// day for nothing. Its predicate is
        /// <c>MetadataStamp != ContentStamp OR ThumbnailStamp != ContentStamp</c> - neither column is
        /// indexable - so every one of those passes walked all 25,504 rows, roughly 1,700 page touches
        /// every fifteen seconds, forever. That keeps the whole <c>Files</c> table hot, which on a NAS is
        /// the exact opposite of letting a disc spin down, and the acoustic goal is what the byte cache
        /// exists to serve.
        /// <para>
        /// Four minutes is the trade being made, and it is a real one: on an otherwise idle server a
        /// newly copied file can now wait this long for its thumbnail. The first pass that finds work
        /// resets the wait to <see cref="_idleDelay"/> and then to <see cref="_busyDelay"/>, so a bulk
        /// import pays the delay once rather than per file. Browsing and streaming are unaffected - they
        /// never wait on this service.
        /// </para>
        /// </remarks>
        private static readonly TimeSpan _maxIdleDelay = TimeSpan.FromMinutes(4);

        /// <summary>
        /// Whether a run has done work that has not yet been settled.
        /// </summary>
        /// <remarks>
        /// Only ever touched from the single loop in <c>ExecuteAsync</c>, so it needs no synchronisation.
        /// Without it an already-idle server would settle on every wake, forcing a compacting collection
        /// on a process with nothing to give back.
        /// </remarks>
        private bool _hasProcessedSinceSettle;

        /// <summary>
        /// Files that failed recently, and when each may be tried again.
        /// </summary>
        /// <remarks>
        /// While work is queued the passes run a second apart, so all <see cref="MaxFailureCount"/>
        /// attempts used to land within about three seconds - too soon for anything transient to have
        /// cleared, and three full decodes and three warnings for a file that can never be read. In memory
        /// on purpose: a restart is a fair moment for a fresh attempt, and it needs no schema change.
        /// Written by both workers, hence concurrent.
        /// </remarks>
        private readonly ConcurrentDictionary<Guid, DateTimeOffset> _retryNotBefore = new();

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly IServedFileCache _fileCache;
        private readonly IDatabaseReadySignal _readySignal;
        private readonly ILogger<MediaProcessingHostedService> _logger;

        public MediaProcessingHostedService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<DlnaOptions> options,
            TimeProvider timeProvider,
            IServedFileCache fileCache,
            IDatabaseReadySignal readySignal,
            ILogger<MediaProcessingHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _timeProvider = timeProvider;
            _fileCache = fileCache;
            _readySignal = readySignal;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // The first batch query needs a schema - see IDatabaseReadySignal.
                await _readySignal.WaitAsync(stoppingToken);

                var emptyPasses = 0;

                while (!stoppingToken.IsCancellationRequested)
                {
                    var processed = 0;

                    try
                    {
                        processed = await ProcessBatchAsync(stoppingToken);

                        if (processed > 0)
                        {
                            _hasProcessedSinceSettle = true;
                        }
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        // A whole pass failed - claiming the batch, or the database itself. Individual
                        // files are already guarded, so this is the repository or a defect. Either way
                        // the service keeps its loop: the next pass re-claims the same work.
                        LogPassFailed(exception);
                    }

                    // The transition from working to idle, and the only moment the run is known to be
                    // over. Not after every batch - that would collect 1,020 times over a 25,504-file
                    // library, which is the reference's mistake with extra steps.
                    if (processed == 0 && emptyPasses == 0 && _hasProcessedSinceSettle)
                    {
                        await SettleAsync(stoppingToken);
                    }

                    // Back off when there is nothing to do, so an idle server is not waking constantly.
                    // Geometric rather than fixed: see the remark on _maxIdleDelay for why a settled
                    // library at a fixed 15 seconds was a measurable cost rather than a tidiness point.
                    emptyPasses = processed == 0 ? emptyPasses + 1 : 0;

                    await Task.Delay(
                        processed == 0 ? ResolveIdleDelay(emptyPasses) : _busyDelay,
                        _timeProvider,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        /// <remarks>
        /// Doubling by shifting the base delay, capped. <c>emptyPasses</c> is bounded by the cap check
        /// rather than by the counter, so a server left idle for a week cannot overflow the shift - the
        /// exponent is clamped before it is used.
        /// <para>
        /// <c>internal</c> rather than <c>private</c> so the curve can be asserted directly. The loop
        /// that calls it is a <c>BackgroundService</c> with a <c>TimeProvider</c>, and driving the whole
        /// loop to assert one delay proves far less than reading the schedule off the function.
        /// </para>
        /// </remarks>
        internal static TimeSpan ResolveIdleDelay(int emptyPasses)
        {
            if (emptyPasses <= EmptyPassesBeforeBackoff)
            {
                return _idleDelay;
            }

            var doublings = Math.Min(emptyPasses - EmptyPassesBeforeBackoff, MaxDoublings);
            var delay = _idleDelay * (1 << doublings);

            return delay > _maxIdleDelay ? _maxIdleDelay : delay;
        }

        /// <remarks>
        /// <c>internal</c> for the same reason as <see cref="ResolveIdleDelay"/>: the schedule is asserted
        /// directly rather than by driving the loop through half an hour of fake time.
        /// </remarks>
        internal static TimeSpan ResolveRetryDelay(int failuresSoFar)
        {
            return failuresSoFar <= 1 ? _firstRetryDelay : _laterRetryDelay;
        }

        private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<MediaFileDto> pending;
            var notYetRetryable = CollectNotYetRetryable();

            // Its own scope, disposed before the workers start. Claiming the batch is one short query and
            // holding a DbContext open across the whole pass would keep a pooled connection - and its
            // page cache - for the minutes the previews take.
            using (var claimScope = _scopeFactory.CreateScope())
            {
                var claimFiles = claimScope.ServiceProvider.GetRequiredService<IMediaFileRepository>();
                pending = await claimFiles.GetPendingProcessingAsync(
                    BatchSize,
                    MaxFailureCount,
                    notYetRetryable,
                    cancellationToken);
            }

            if (pending.Count == 0)
            {
                return 0;
            }

            var options = _options.CurrentValue;
            var settings = BuildSettings(options);
            var queue = new ConcurrentQueue<MediaFileDto>(pending);
            var workerCount = Math.Min(ResolveWorkerCount(), pending.Count);

            var workers = new Task[workerCount];

            for (var worker = 0; worker < workerCount; worker++)
            {
                workers[worker] = DrainAsync(queue, options, settings, cancellationToken);
            }

            await Task.WhenAll(workers);

            return pending.Count;
        }

        /// <remarks>
        /// One scope per worker, and this is not optional. A scoped <c>DlnaDbContext</c> is single-threaded
        /// - two workers sharing one would throw <c>A second operation was started on this context
        /// instance</c>, which is the same failure the preview page's gate exists for.
        /// </remarks>
        private async Task DrainAsync(
            ConcurrentQueue<MediaFileDto> queue,
            DlnaOptions options,
            MediaProcessingSettings settings,
            CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();
            var processor = scope.ServiceProvider.GetRequiredService<IMediaProcessor>();

            while (queue.TryDequeue(out var file))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await ProcessOneAsync(file, files, processor, options, settings, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // One unreadable file must never end the pass. The per-phase guards below already
                    // convert a failed probe or encode into a recorded failure, so reaching this means
                    // the database call failed - which the next pass will retry.
                    LogFileFailed(file.FullPath, exception);
                }
            }
        }

        /// <summary>
        /// Gives back the memory a processing run borrowed, once the run is over.
        /// </summary>
        /// <remarks>
        /// <b>This forces a collection, and section 3 of <c>docs/decisions.md</c> forbids that.</b> The exception is
        /// deliberate and narrow. What that rule bans is the reference's shape - a collect on every
        /// eviction, through an unbounded chain, which is what 727 Gen2 collections in twelve days are.
        /// This runs <b>once per completed run</b>: the edge where the queue drains after real work, so a
        /// full 25,504-file first pass triggers it once rather than a thousand times.
        /// <para>
        /// It exists because the memory a run borrows does not come back on its own. Skia decodes through
        /// the C allocator in chunks below the deployment's mmap threshold, so freeing them returns the
        /// pages to a glibc arena and no further; the managed side leaves a large object heap the
        /// collector will not compact unless asked; and every worker's scope has left a pooled SQLite
        /// connection holding its page cache. Three different holders, none of which a quiet server
        /// releases, which is why all three steps are here and in this order.
        /// </para>
        /// <para>
        /// Failure is swallowed on purpose. This is housekeeping after the useful work has already been
        /// committed, and there is nothing a caller could usefully do about it.
        /// </para>
        /// </remarks>
        private async Task SettleAsync(CancellationToken cancellationToken)
        {
            _hasProcessedSinceSettle = false;

            using var process = Process.GetCurrentProcess();
            var beforeBytes = process.WorkingSet64;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var maintenance = scope.ServiceProvider.GetRequiredService<IIndexMaintenance>();

                await maintenance.ReleaseMemoryAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogSettleFailed(exception);
            }

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

            _ = NativeHeapTrimmer.TryTrim();

            process.Refresh();

            LogSettled(
                beforeBytes / BytesPerMegabyte,
                process.WorkingSet64 / BytesPerMegabyte);
        }

        /// <remarks>
        /// Half the cores, never more than two. Each worker can hold a full-resolution decode - Skia is
        /// allowed 24 million pixels at 4 bytes each - so this is bounded by memory rather than by
        /// throughput, and the NAS this targets reports four cores. One worker on anything smaller,
        /// which restores the old behaviour exactly.
        /// </remarks>
        private static int ResolveWorkerCount()
        {
            return Math.Clamp(Environment.ProcessorCount / 2, 1, MaxWorkers);
        }

        private async Task ProcessOneAsync(
            MediaFileDto file,
            IMediaFileRepository files,
            IMediaProcessor processor,
            DlnaOptions options,
            MediaProcessingSettings settings,
            CancellationToken cancellationToken)
        {
            // The suppression flags are ANDed in, not just the stamps. GetPendingProcessingAsync honours
            // them but is an OR across the two concerns, so a metadata-suppressed file returned for its
            // thumbnail used to have its metadata re-extracted anyway - and SaveMetadataAsync left the
            // flag set, so "clear metadata" silently undid itself on the next pass.
            var metadataNeeded = !file.IsMetadataSuppressed && file.MetadataStamp != file.ContentStamp;
            var thumbnailNeeded = !file.IsThumbnailSuppressed
                && file.ThumbnailStamp != file.ContentStamp
                && IsThumbnailWanted(file, options);

            var metadataFailed = false;
            var thumbnailFailed = false;

            // Carried from the metadata pass to the thumbnail pass so the same file is not probed twice
            // in one visit - the second probe existed only to re-read this value.
            TimeSpan? knownDuration = null;

            // An audio file's cover image is carried as a video stream, so "has a video stream" is what
            // says whether there is any artwork to extract. Seeded from the last pass's stored metadata
            // and refreshed below when this visit reads it again.
            var hasEmbeddedArtwork = file.Width is not null;

            if (metadataNeeded)
            {
                try
                {
                    var metadata = await processor.ExtractMetadataAsync(
                        file.FullPath,
                        file.Mime,
                        settings,
                        cancellationToken);

                    if (metadata is null)
                    {
                        metadataFailed = true;
                    }
                    else
                    {
                        knownDuration = metadata.Video?.Duration;
                        hasEmbeddedArtwork = metadata.Video is not null;

                        await files.SaveMetadataAsync(file.PublicId, metadata, file.ContentStamp, cancellationToken);
                        LogMetadataStored(file.FullPath);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // A throw and a null both mean "this file has no metadata we could read". Treating
                    // them the same is what lets MaxFailureCount retire the file: before this, a thrown
                    // failure bypassed the counter entirely and the file was re-probed on every pass.
                    LogMetadataFailed(file.FullPath, exception);
                    metadataFailed = true;
                }
            }

            // Most of a music library carries no cover image, and a file that has none has nothing to
            // make rather than something that failed. Dropped to "not wanted" so it takes the
            // not-applicable branch below and stops coming back, instead of burning MaxFailureCount
            // passes and then being retired as a failure.
            if (thumbnailNeeded && !hasEmbeddedArtwork && file.Mime.ToMedia() == DlnaMedia.Audio)
            {
                thumbnailNeeded = false;
            }

            if (thumbnailNeeded)
            {
                try
                {
                    var targetPath = BuildThumbnailPath(file, options, settings);

                    var thumbnail = await processor.GenerateThumbnailAsync(
                        file.FullPath,
                        file.Mime,
                        targetPath,
                        settings,

                        // The parameter existed and nothing ever passed false, so the guard its own
                        // remark describes did not exist: "Recreate thumbnails" cleared the row, the
                        // image beside the media was adopted straight back, and a raised MaxWidth never
                        // took effect while the UI reported success.
                        allowAdoption: !file.IsThumbnailRebuildForced,
                        notOlderThanUtc: file.FileModifiedUtc,
                        knownDuration,
                        cancellationToken);

                    if (thumbnail is null)
                    {
                        thumbnailFailed = true;
                    }
                    else
                    {
                        await files.SaveThumbnailAsync(file.PublicId, thumbnail, file.ContentStamp, cancellationToken);

                        if (!thumbnail.WasAdopted)
                        {
                            // A generated thumbnail overwrites the file at a path the cache may already
                            // hold. The cache is keyed by path and knows nothing about content, so
                            // without this the previous image kept being served - and because every
                            // browse refreshes the sliding window, indefinitely in a folder anyone was
                            // looking at. Adopted thumbnails are the file already on disc, so nothing
                            // changed and there is nothing to drop.
                            _ = _fileCache.Evict(thumbnail.FilePath);
                        }

                        if (thumbnail.WasAdopted)
                        {
                            LogThumbnailAdopted(file.FullPath);
                        }
                        else
                        {
                            LogThumbnailCreated(file.FullPath, thumbnail.Width, thumbnail.Height);
                        }
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogThumbnailFailed(file.FullPath, exception);
                    thumbnailFailed = true;
                }
            }
            else if (!thumbnailNeeded && file.ThumbnailStamp != file.ContentStamp)
            {
                // No thumbnail is wanted for this kind of file. Record the attempt so the pending query
                // stops returning it - the reference left audio files pending forever for this reason.
                await files.MarkThumbnailNotApplicableAsync(file.PublicId, cancellationToken);
            }

            if (metadataFailed || thumbnailFailed)
            {
                await files.RecordProcessingFailureAsync(
                    file.PublicId,
                    metadataFailed,
                    thumbnailFailed,
                    cancellationToken);

                // The counts on the claimed row are from before this attempt, hence the + 1.
                var failuresSoFar = 1 + Math.Max(
                    metadataFailed ? file.MetadataFailureCount : 0,
                    thumbnailFailed ? file.ThumbnailFailureCount : 0);

                _retryNotBefore[file.PublicId] = _timeProvider.GetUtcNow() + ResolveRetryDelay(failuresSoFar);
            }
        }

        /// <summary>
        /// The files still waiting out their retry delay, dropping those whose delay has passed.
        /// </summary>
        private List<Guid> CollectNotYetRetryable()
        {
            var now = _timeProvider.GetUtcNow();
            var waiting = new List<Guid>(_retryNotBefore.Count);

            foreach (var (publicId, notBefore) in _retryNotBefore)
            {
                if (notBefore > now)
                {
                    waiting.Add(publicId);
                }
                else
                {
                    _ = _retryNotBefore.TryRemove(publicId, out _);
                }
            }

            return waiting;
        }

        private static bool IsThumbnailWanted(MediaFileDto file, DlnaOptions options)
        {
            return file.Mime.ToMedia() switch
            {
                DlnaMedia.Video => options.Thumbnails.GenerateForVideo,
                DlnaMedia.Image => options.Thumbnails.GenerateForImages,
                DlnaMedia.Audio => options.Thumbnails.GenerateForAudio,
                _ => false,
            };
        }

        /// <summary>
        /// Thumbnails are named by the file's identifier, inside the cache directory.
        /// </summary>
        /// <remarks>
        /// Not derived from the media path: the reference wrote thumbnails into the media tree itself and
        /// relied on an exclusion entry to stop re-indexing them as media.
        /// </remarks>
        private static string BuildThumbnailPath(
            MediaFileDto file,
            DlnaOptions options,
            MediaProcessingSettings settings)
        {
            var extensions = settings.ThumbnailMime.ToFileExtensions();
            var extension = extensions.Count > 0 ? extensions[0] : ThumbnailPath.DefaultExtension;

            return ThumbnailPath.Resolve(file, options, extension);
        }

        private static MediaProcessingSettings BuildSettings(DlnaOptions options)
        {
            return new MediaProcessingSettings(
                options.Thumbnails.MaxWidth,
                options.Thumbnails.MaxHeight,
                options.Thumbnails.Quality,
                DlnaMime.ImageJpeg,
                options.Thumbnails.StoreInDatabase,
                options.Thumbnails.DownloadFFmpeg,
                options.Library.ReadContainerTags);
        }
    }
}
