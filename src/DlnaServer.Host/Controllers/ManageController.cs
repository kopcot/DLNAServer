using System.Data.Common;
using System.Diagnostics;
using System.Runtime;
using System.Globalization;
using CommunityToolkit.HighPerformance;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Hosting;
using DlnaServer.Host.Delivery.Caching;
using DlnaServer.Host.Diagnostics;
using DlnaServer.Host.Gena;
using DlnaServer.Persistence;
using DlnaServer.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using DlnaServer.Core.Delivery;
using DlnaServer.Core.Diagnostics;
using DlnaServer.Core.Gena;

namespace DlnaServer.Host.Controllers
{
    /// <summary>
    /// Diagnostics and management endpoints.
    /// </summary>
    /// <remarks>
    /// Unauthenticated, like the reference, and reachable on the media port. That is a deliberate
    /// decision rather than an inherited one: the server is expected to be on a trusted LAN, and
    /// <see cref="Stop"/> means any device that can browse the library can also shut the server down. On
    /// an untrusted network this controller needs a guard before anything else does.
    /// <para>
    /// Every endpoint that changes state is <c>POST</c>, and that is load-bearing rather than tidiness.
    /// The trusted-LAN argument covers requests a LAN device makes deliberately; it does not cover a
    /// request the operator's own browser is tricked into making on behalf of an outside page. While
    /// these were <c>GET</c>, any web page could carry
    /// <c>&lt;img src="http://nas:26852/manage/stop"&gt;</c> and stop the server, and a link unfurler or a
    /// browser prefetch would do it by accident.
    /// </para>
    /// <para>
    /// The verb is <b>not</b> the whole defence, and an earlier version of this remark said it was.
    /// CORS decides whether an attacker may read the response, never whether the browser sends the
    /// request: a cross-origin form <c>POST</c> is a CORS-simple request that arrives and executes.
    /// <see cref="RejectCrossSiteEndpointFilter"/> is what actually closes that, on every controller
    /// rather than on this one.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("manage")]
    public sealed partial class ManageController : ControllerBase
    {
        private const double BytesPerMegabyte = 1024d * 1024d;

        private const int DefaultPageSize = 100;
        private const int MaxPageSize = 1_000;

        /// <summary>
        /// Generation names in the fixed order the runtime reports them.
        /// </summary>
        private static readonly string[] _generationNames = ["gen0", "gen1", "gen2", "loh", "poh"];

        private readonly IHostApplicationLifetime _lifetime;
        private readonly IRestartSignal _restartSignal;
        private readonly IDatabaseResetSignal _databaseResetSignal;
        private readonly IServedFileCache _fileCache;
        private readonly IApiBlocker _blocker;
        private readonly ISubscriptionStore _subscriptions;
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly ILogger<ManageController> _logger;

        public ManageController(
            IHostApplicationLifetime lifetime,
            IRestartSignal restartSignal,
            IDatabaseResetSignal databaseResetSignal,
            IServedFileCache fileCache,
            IApiBlocker blocker,
            ISubscriptionStore subscriptions,
            IOptionsMonitor<DlnaOptions> options,
            ILogger<ManageController> logger)
        {
            _lifetime = lifetime;
            _restartSignal = restartSignal;
            _databaseResetSignal = databaseResetSignal;
            _fileCache = fileCache;
            _blocker = blocker;
            _subscriptions = subscriptions;
            _options = options;
            _logger = logger;
        }

        /// <summary>
        /// Reports process and GC memory.
        /// </summary>
        /// <remarks>
        /// Field names mirror the reference's <c>/manage/memory</c> so before-and-after numbers can be
        /// compared directly against the live server. Working set is the figure that matters on a NAS:
        /// the reference sits around 395 MB against a 42 MB managed heap, so most of its footprint is
        /// native rather than C# objects.
        /// </remarks>
        [HttpGet("memory")]
        public IActionResult GetMemory()
        {
            using var process = Process.GetCurrentProcess();
            var gc = GC.GetGCMemoryInfo();

            return Ok(new
            {
                ProcessId = process.Id,
                StartTimeUtc = process.StartTime.ToUniversalTime(),
                UptimeSeconds = Math.Round((DateTime.Now - process.StartTime).TotalSeconds, 1),

                WorkingSetMb = Round(process.WorkingSet64),
                PrivateMemoryMb = Round(process.PrivateMemorySize64),
                ManagedHeapMb = Round(GC.GetTotalMemory(forceFullCollection: false)),
                GcHeapSizeMb = Round(gc.HeapSizeBytes),
                GcTotalCommittedMb = Round(gc.TotalCommittedBytes),

                VirtualMemoryMb = Round(process.VirtualMemorySize64),

                // What the machine has and how loaded it is. The byte cache clamps its budget to half of
                // TotalAvailableMemory, so this is the number that says whether the configured limit or
                // the clamp is in force.
                TotalAvailableMemoryMb = Round(gc.TotalAvailableMemoryBytes),
                MemoryLoadMb = Round(gc.MemoryLoadBytes),

                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),

                IsServerGc = System.Runtime.GCSettings.IsServerGC,
                LatencyMode = System.Runtime.GCSettings.LatencyMode.ToString(),

                // The name is misleading and the field is kept only because the reference's shape is.
                // GCMemoryInfo.Concurrent describes the LAST collection - whether it was a background
                // one - not whether concurrent GC is enabled. Gen0 and gen1 collections are never
                // background, so on a healthy server this reads False almost always, and it read False
                // on the NAS while System.GC.Concurrent was true. The 2026-09-03 review took that as
                // evidence the setting was not taking effect; it is not. There is no runtime API that
                // reports the configured value - read System.GC.Concurrent out of
                // DlnaServer.Host.runtimeconfig.json in the deployment folder instead.
                IsConcurrentGc = gc.Concurrent,
                LohCompactionMode = System.Runtime.GCSettings.LargeObjectHeapCompactionMode.ToString(),
                PinnedObjectsCount = gc.PinnedObjectsCount,

                // Server GC reserves a heap and a thread per core, so this is what explains a committed
                // figure several times the live heap.
                ProcessorCount = Environment.ProcessorCount,
                ThreadCount = process.Threads.Count,

                GcIndex = gc.Index,
                GcGeneration = gc.Generation,
                Generations = ReadGenerations(gc),
            });
        }

        /// <summary>
        /// Reports what the served-bytes cache holds, including every cached path.
        /// </summary>
        /// <remarks>
        /// Mirrors the reference's <c>/Manage/memoryCache</c>. The paths are the point: a byte total says
        /// how much is held, the listing says what, and only the second answers whether a 974 MB large
        /// object heap is the cache doing its job or something else entirely.
        /// </remarks>
        [HttpGet("filecache")]
        public IActionResult GetFileCache()
        {
            var report = _fileCache.Describe();

            return Ok(new
            {
                report.IsEnabled,
                BudgetMb = Round(report.BudgetInBytes),
                HeldMb = Round(report.BytesHeld),
                MaxFileSizeMb = Round(report.MaxFileSizeInBytes),
                report.EntryCount,
                report.Hits,
                report.Misses,

                // Carried alongside Misses rather than subtracted from it, because this endpoint reports
                // what was counted and the subtraction is an upper bound - see the property's own remark.
                // The Recently-served page is what turns the pair into a disc figure.
                report.DatabaseHits,
                report.ReadsInFlight,
                Paths = _fileCache.ListPaths(),
            });
        }

        /// <summary>
        /// Drops every cached payload and asks the runtime and the C allocator for the memory back.
        /// </summary>
        /// <remarks>
        /// This is the one place a collection is forced, and it is deliberate. Section 3 of
        /// <c>docs/decisions.md</c> forbids forcing a GC, and that rule stands for the automatic case - the
        /// reference collects on <b>every eviction</b> through an unbounded <c>Task.Run</c> chain, which
        /// is what its 727 Gen2 collections in twelve days are. An operator asking for the memory back is
        /// a different thing: cached payloads above 85 KB live on the large object heap, which is
        /// reclaimed only on a Gen2 collection and never compacted by default, so clearing the entries
        /// without collecting would report success and return nothing to the operating system. Measured
        /// 2026-09-02: 974 MB of unfragmented large-object heap survived 42 minutes and 3 Gen2 collections.
        /// <para>
        /// The managed heap is only half of it, and on this server the smaller half. Measured on the NAS
        /// 2026-09-06, the process held a 562 MB working set against 44 MB of GC-committed bytes, so
        /// roughly 518 MB was native - most of it SQLite page cache held by pooled connections that
        /// nothing releases. The three steps therefore run in order and each one is needed: free the
        /// pooled page caches to the allocator, collect and compact the managed heap, then trim the
        /// allocator's arenas so the operating system actually gets the pages back.
        /// </para>
        /// <para>
        /// Working set is reported either side of all three because it is the only figure that reflects
        /// every step - the managed numbers cannot see the SQLite or allocator halves at all.
        /// </para>
        /// </remarks>
        [HttpPost("filecache/clear")]
        public async Task<IActionResult> ClearFileCache(
            [FromServices] IIndexMaintenance maintenance,
            CancellationToken cancellationToken)
        {
            using var process = Process.GetCurrentProcess();

            var cleared = _fileCache.Clear();
            var beforeMb = Round(GC.GetTotalMemory(forceFullCollection: false));
            var workingSetBeforeMb = Round(process.WorkingSet64);

            await maintenance.ReleaseMemoryAsync(cancellationToken);

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

            var trimmed = NativeHeapTrimmer.TryTrim();

            LogFileCacheCleared(cleared, HttpContext.Connection.RemoteIpAddress?.ToString());

            process.Refresh();

            return Ok(new
            {
                EntriesCleared = cleared,
                ManagedBeforeMb = beforeMb,
                ManagedAfterMb = Round(GC.GetTotalMemory(forceFullCollection: false)),
                WorkingSetBeforeMb = workingSetBeforeMb,
                WorkingSetAfterMb = Round(process.WorkingSet64),
                NativeHeapTrimmed = trimmed,
            });
        }

        /// <summary>
        /// The configuration actually in force, after defaults and validation.
        /// </summary>
        /// <remarks>
        /// Mirrors the reference's <c>/Manage/configuration</c>. Read through
        /// <see cref="IOptionsMonitor{TOptions}"/> rather than from the file, so what it reports is what
        /// the running server is using - including a value that arrived from an environment variable or a
        /// hot reload rather than from <c>config.json</c>.
        /// </remarks>
        [HttpGet("configuration")]
        public IActionResult GetConfiguration()
        {
            return Ok(_options.CurrentValue);
        }

        /// <summary>
        /// Row counts for the indexed library.
        /// </summary>
        /// <remarks>
        /// Counts rather than rows: the reference's <c>/Manage/database</c> dumps whole tables, which on a
        /// 25,000-file library is a response no browser wants and a great deal of memory to build. Its
        /// per-entity dumps are a separate, paged concern.
        /// </remarks>
        [HttpGet("database")]
        public async Task<IActionResult> GetDatabaseAsync(
            [FromServices] IMediaFileRepository files,
            [FromServices] IMediaDirectoryRepository directories,
            CancellationToken cancellationToken)
        {
            return Ok(new
            {
                Files = await files.CountAsync(cancellationToken),
                Directories = await directories.CountAsync(cancellationToken),
            });
        }

        /// <summary>
        /// The live GENA subscriptions.
        /// </summary>
        /// <remarks>
        /// The reference has this endpoint but returns an empty <c>Ok()</c>, so the subscriptions it holds
        /// are invisible. This reports them, which also makes the store observable for the first time -
        /// nothing else reads it, since no NOTIFY is pushed.
        /// </remarks>
        [HttpGet("subscriptions")]
        public IActionResult GetSubscriptions()
        {
            var subscriptions = _subscriptions.List();

            return Ok(new
            {
                subscriptions.Count,
                Subscriptions = subscriptions,
            });
        }

        /// <summary>
        /// A page of indexed files, ordered by path.
        /// </summary>
        /// <remarks>
        /// Paged by <c>after</c> rather than by an offset, so a page is stable while the library is being
        /// scanned: an offset shifts when a row is inserted ahead of it, and a full scan inserts
        /// thousands. The reference's equivalent returns every row at once, which on 25,501 files is a
        /// response measured in tens of megabytes.
        /// </remarks>
        [HttpGet("file")]
        public async Task<IActionResult> GetFilesAsync(
            [FromServices] IMediaFileRepository files,
            CancellationToken cancellationToken,
            [FromQuery] string? after = null,
            [FromQuery] int take = DefaultPageSize)
        {
            var page = await files.GetIndexedPageAsync(after, ClampPageSize(take), cancellationToken);

            return Ok(new
            {
                Count = page.Count,
                NextAfter = page.Count == 0 ? null : page[^1].FullPath,
                Files = page,
            });
        }

        /// <summary>
        /// One file with its streams and thumbnail.
        /// </summary>
        [HttpGet("file/{id:guid}")]
        public async Task<IActionResult> GetFileAsync(
            [FromRoute] Guid id,
            [FromServices] IMediaFileRepository files,
            CancellationToken cancellationToken)
        {
            var details = await files.GetWithDetailsAsync(id, cancellationToken);

            return details is null ? NotFound() : Ok(details);
        }

        /// <summary>
        /// The most recently indexed files, newest first.
        /// </summary>
        [HttpGet("fileLast")]
        public async Task<IActionResult> GetRecentFilesAsync(
            [FromServices] IMediaFileRepository files,
            CancellationToken cancellationToken,
            [FromQuery] int count = DefaultPageSize)
        {
            return Ok(await files.GetRecentlyAddedAsync(ClampPageSize(count), cancellationToken));
        }

        /// <summary>
        /// A page of indexed directories, ordered by path.
        /// </summary>
        [HttpGet("directory")]
        public async Task<IActionResult> GetDirectoriesAsync(
            [FromServices] IMediaDirectoryRepository directories,
            CancellationToken cancellationToken,
            [FromQuery] string? after = null,
            [FromQuery] int take = DefaultPageSize)
        {
            var page = await directories.GetIndexedPageAsync(after, ClampPageSize(take), cancellationToken);

            return Ok(new
            {
                Count = page.Count,
                NextAfter = page.Count == 0 ? null : page[^1].FullPath,
                Directories = page,
            });
        }

        /// <summary>
        /// One directory with a page of its child directories and a page of its files.
        /// </summary>
        /// <remarks>
        /// Paged because a single folder can hold thousands of files, and the unpaged reads materialised
        /// every one of them into one response. <c>take</c> defaults to the cap rather than to
        /// <see cref="DefaultPageSize"/>, so a call without parameters still gets a whole ordinary folder,
        /// and the two totals say when there is more to fetch.
        /// <para>
        /// The two listings page independently: <c>skipChildren</c> and <c>skipFiles</c> move each one on its
        /// own, and <c>skip</c> is the offset for whichever of them is not given - so a caller that pages
        /// with <c>skip</c> alone moves both together, as before.
        /// </para>
        /// </remarks>
        [HttpGet("directory/{id:guid}")]
        public async Task<IActionResult> GetDirectoryAsync(
            [FromRoute] Guid id,
            [FromServices] IMediaDirectoryRepository directories,
            [FromServices] IMediaFileRepository files,
            CancellationToken cancellationToken,
            [FromQuery] int skip = 0,
            [FromQuery] int take = MaxPageSize,
            [FromQuery] int? skipChildren = null,
            [FromQuery] int? skipFiles = null)
        {
            var directory = await directories.GetByPublicIdAsync(id, cancellationToken);

            if (directory is null)
            {
                return NotFound();
            }

            var childOffset = Math.Max(skipChildren ?? skip, 0);
            var fileOffset = Math.Max(skipFiles ?? skip, 0);
            var pageSize = ClampPageSize(take);

            return Ok(new
            {
                Directory = directory,
                ChildCount = await directories.CountChildrenAsync(id, cancellationToken),
                FileCount = await files.CountByDirectoryAsync(id, cancellationToken),
                Children = await directories.GetChildrenPageAsync(
                    id,
                    childOffset,
                    pageSize,
                    sortByDate: false,
                    descending: false,
                    cancellationToken),
                Files = await files.GetByDirectoryPageAsync(
                    id,
                    fileOffset,
                    pageSize,
                    sortByDate: false,
                    descending: false,
                    cancellationToken),
            });
        }

        /// <summary>
        /// The video stream metadata of one file.
        /// </summary>
        /// <remarks>
        /// Per file rather than a whole-table dump. The reference lists every video stream in the library
        /// in one response; the same information is reachable here through the file it belongs to, which
        /// is also the only form in which it means anything.
        /// </remarks>
        [HttpGet("videoMetadata/{id:guid}")]
        public async Task<IActionResult> GetVideoMetadataAsync(
            [FromRoute] Guid id,
            [FromServices] IMediaFileRepository files,
            CancellationToken cancellationToken)
        {
            var details = await files.GetWithDetailsAsync(id, cancellationToken);

            if (details is null)
            {
                return NotFound();
            }

            return Ok(new
            {
                details.File.FullPath,
                details.Video,
                details.AudioStreams,
                details.Subtitles,
            });
        }

        /// <summary>
        /// A page of generated thumbnails, ordered by the media path they belong to.
        /// </summary>
        [HttpGet("thumbnail")]
        public async Task<IActionResult> GetThumbnailsAsync(
            [FromServices] IMediaFileRepository files,
            CancellationToken cancellationToken,
            [FromQuery] string? after = null,
            [FromQuery] int take = DefaultPageSize)
        {
            var page = await files.GetThumbnailPageAsync(after, ClampPageSize(take), cancellationToken);

            return Ok(new
            {
                Count = page.Count,

                // The MEDIA file's path, because that is what the page is ordered and sought by - the
                // thumbnail's own FilePath is the .@__thumb image and paging on it would walk a
                // different sequence entirely.
                NextAfter = page.Count == 0 ? null : page[^1].MediaFileFullPath,
                Thumbnails = page,
            });
        }

        /// <summary>
        /// One thumbnail's metadata, by the thumbnail's own identifier.
        /// </summary>
        /// <remarks>
        /// The thumbnail's identifier, not the media file's - the repository looks these up by
        /// <c>Thumbnails.PublicId</c>. A file's thumbnail identifier is on its
        /// <see cref="DlnaServer.Core.Contracts.MediaFileDto.ThumbnailPublicId"/>, and confusing the two
        /// produces a 404 that looks exactly like a missing thumbnail.
        /// </remarks>
        [HttpGet("thumbnail/{id:guid}")]
        public async Task<IActionResult> GetThumbnailAsync(
            [FromRoute] Guid id,
            [FromServices] IMediaFileRepository files,
            CancellationToken cancellationToken)
        {
            var thumbnail = await files.GetThumbnailByPublicIdAsync(id, cancellationToken);

            return thumbnail is null ? NotFound() : Ok(thumbnail);
        }

        /// <summary>
        /// The stored bytes of one thumbnail.
        /// </summary>
        /// <remarks>
        /// One at a time, by the <b>thumbnail's</b> identifier. The reference's <c>thumbnailData</c> dumps
        /// every stored blob in the library in a single response, which on a previewed library is
        /// gigabytes of base64 - and on a 2-4 GB machine that is not a diagnostic, it is an outage.
        /// </remarks>
        [HttpGet("thumbnailData/{id:guid}")]
        public async Task<IActionResult> GetThumbnailDataAsync(
            [FromRoute] Guid id,
            [FromServices] IMediaFileRepository files,
            CancellationToken cancellationToken)
        {
            var thumbnail = await files.GetThumbnailByPublicIdAsync(id, cancellationToken);

            if (thumbnail is null)
            {
                return NotFound();
            }

            var content = await files.GetThumbnailContentAsync(id, cancellationToken);

            if (content is null)
            {
                return NotFound("The thumbnail has no copy stored in the database.");
            }

            return File(content.AsMemory().AsStream(), thumbnail.Mime.ToMimeString());
        }

        /// <summary>
        /// The MIME types the server recognises, with their DLNA profiles and extensions.
        /// </summary>
        /// <remarks>
        /// Static: this is the compiled-in catalogue, not configuration. It answers "why is this file not
        /// served" faster than reading the source, which is the reference's reason for having it too.
        /// </remarks>
        [HttpGet("dlnaMime")]
        public IActionResult GetDlnaMimeCatalog()
        {
            return Ok(DlnaMimeCatalog.All);
        }

        /// <summary>
        /// Forgets all extracted metadata so it is read again.
        /// </summary>
        [HttpPost("clearAllMetadata")]
        public async Task<IActionResult> ClearAllMetadataAsync(
            [FromServices] IMediaFileRepository files,
            CancellationToken cancellationToken)
        {
            var affected = await files.ClearAllMetadataAsync(cancellationToken);

            LogMaintenance(nameof(ClearAllMetadataAsync), affected, HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { FilesAffected = affected });
        }

        /// <summary>
        /// Removes every thumbnail record so thumbnails are produced again.
        /// </summary>
        /// <remarks>
        /// The image files beside the media are left in place. They are the reference's layout, a rescan
        /// adopts them, and deleting them would turn a cheap reset into a full ffmpeg pass over the
        /// library - which is what <c>recreateAllFilesInfo</c> is for.
        /// </remarks>
        [HttpPost("clearAllThumbnails")]
        public async Task<IActionResult> ClearAllThumbnailsAsync(
            [FromServices] IMediaFileRepository files,
            CancellationToken cancellationToken)
        {
            var affected = await files.ClearAllThumbnailsAsync(cancellationToken);

            LogMaintenance(nameof(ClearAllThumbnailsAsync), affected, HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { FilesAffected = affected });
        }

        /// <summary>
        /// Forgets both metadata and thumbnails for the whole library.
        /// </summary>
        /// <remarks>
        /// Returns as soon as the records are cleared; the background processing pass does the work, one
        /// file at a time, and its progress is visible in the log. The reference instead holds the request
        /// open for the entire rebuild and blocks the whole API while it runs.
        /// <para>
        /// The two clears are <b>not</b> one transaction, and the response says which half committed for
        /// that reason. They are separate bulk <c>ExecuteUpdate</c> statements, so a failure on the second
        /// leaves the first committed - and the halves do not recover alike: a cleared thumbnail stamp
        /// makes the background pass regenerate the preview, while metadata whose stamp was never
        /// cleared is simply never re-read. Reporting a bare 500 would leave an operator unable to tell
        /// whether to run it again.
        /// </para>
        /// </remarks>
        [HttpPost("recreateAllFilesInfo")]
        public async Task<IActionResult> RecreateAllFilesInfoAsync(
            [FromServices] IMediaFileRepository files,
            CancellationToken cancellationToken)
        {
            var thumbnails = await files.ClearAllThumbnailsAsync(cancellationToken);
            int metadata;

            try
            {
                metadata = await files.ClearAllMetadataAsync(cancellationToken);
            }
            catch (DbException exception)
            {
                LogPartialMaintenance(nameof(RecreateAllFilesInfoAsync), thumbnails, exception);

                // 500 with a body, deliberately: the thumbnails ARE cleared and will be regenerated, so
                // an operator who reruns this needs to know the first half already happened.
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        MetadataCleared = 0,
                        ThumbnailsCleared = thumbnails,
                        Note = "Thumbnails were cleared and will be regenerated. Metadata was NOT - "
                            + "run this again to clear it.",
                    });
            }

            LogMaintenance(nameof(RecreateAllFilesInfoAsync), metadata, HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new
            {
                MetadataCleared = metadata,
                ThumbnailsCleared = thumbnails,
                Note = "Regeneration runs in the background, one file at a time.",
            });
        }

        /// <summary>
        /// Forgets one file's metadata and thumbnail.
        /// </summary>
        [HttpPost("recreateFilesInfo/{id:guid}")]
        public async Task<IActionResult> RecreateFileInfoAsync(
            [FromRoute] Guid id,
            [FromServices] IMediaFileRepository files,
            CancellationToken cancellationToken)
        {
            var reset = await files.ResetProcessingAsync(id, cancellationToken);

            return reset ? Ok(new { PublicId = id, Reset = true }) : NotFound();
        }

        /// <summary>
        /// Refuses renderer traffic for a period.
        /// </summary>
        /// <remarks>
        /// Returns immediately and the block lapses on its own. The reference awaits the whole period
        /// inside this request, so its call hangs for hours and the block is lost if the connection drops.
        /// <c>/manage</c> stays reachable throughout, which is what makes <c>unblock</c> usable.
        /// </remarks>
        [HttpPost("block/{hours:int:min(1):max(72)}")]
        public IActionResult Block([FromRoute] int hours)
        {
            var duration = TimeSpan.FromHours(hours);

            _blocker.Block(duration, $"Blocked for {hours} hour(s) by {HttpContext.Connection.RemoteIpAddress}");
            LogBlocked(hours, HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { BlockedUntilUtc = _blocker.BlockedUntilUtc, _blocker.Reason });
        }

        /// <summary>
        /// Lifts a block early.
        /// </summary>
        [HttpPost("unblock")]
        public IActionResult Unblock()
        {
            var wasBlocked = _blocker.IsBlocked;

            _blocker.Release();
            LogUnblocked(HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { WasBlocked = wasBlocked });
        }

        /// <summary>
        /// Restarts the server in place.
        /// </summary>
        /// <remarks>
        /// Raises the restart signal and stops the host: <c>Program.Main</c> loops while the signal is
        /// set, disposing the old application and building a fresh one, so configuration is re-read and
        /// every hosted service starts again without the process exiting. In-flight streams are cut off,
        /// which is what restarting means.
        /// </remarks>
        [HttpPost("restart")]
        public IActionResult Restart()
        {
            LogRestartRequested(HttpContext.Connection.RemoteIpAddress?.ToString());

            _restartSignal.RequestRestart();
            _lifetime.StopApplication();

            return Ok("restarting");
        }

        /// <summary>
        /// Shuts the server down.
        /// </summary>
        /// <remarks>
        /// Mirrors the reference's <c>/Manage/stop</c>. The response is sent before the process exits:
        /// <see cref="IHostApplicationLifetime.StopApplication"/> begins a graceful shutdown, so Kestrel
        /// finishes in-flight requests - including this one - and the hosted services get their
        /// cancellation. An in-progress stream is cut off, which is what stopping a server means.
        /// <para>
        /// The restart signal is cleared first. <c>Program.Main</c> rebuilds the host after it stops
        /// whenever a restart was requested, so without this a stop arriving after a restart request
        /// would silently restart instead.
        /// </para>
        /// <para>
        /// Refused with 409 while <i>Recreate database</i> is pending. That request lives only in this
        /// process and is carried out by the restart it asked for, so clearing the restart signal would
        /// drop it without a word - and keeping the signal set would turn this stop into a restart. The
        /// caller is told instead, and can stop the server once it is back.
        /// </para>
        /// </remarks>
        [HttpPost("stop")]
        public IActionResult Stop()
        {
            if (_databaseResetSignal.IsResetRequested)
            {
                LogStopRefusedDuringReset(HttpContext.Connection.RemoteIpAddress?.ToString());

                return Conflict("A database recreate is in progress and restarting the server to finish it. "
                    + "Stop it once it is back.");
            }

            LogStopRequested(HttpContext.Connection.RemoteIpAddress?.ToString());

            _restartSignal.Reset();
            _lifetime.StopApplication();

            return Ok("stopping");
        }

        /// <summary>
        /// Keeps a caller-supplied page size sane, so a management call cannot ask for the whole library.
        /// </summary>
        private static int ClampPageSize(int requested)
        {
            return Math.Clamp(requested, 1, MaxPageSize);
        }

        private static double Round(long bytes)
        {
            return Math.Round(bytes / BytesPerMegabyte, 2);
        }

        /// <summary>
        /// Per-generation size and fragmentation, which is where a large object heap problem is visible.
        /// </summary>
        /// <remarks>
        /// The one report that separates "the cache is holding bytes" from "freed buffers are never
        /// compacted": the LOH row shows size and fragmentation before and after the last collection.
        /// <para>
        /// <see cref="GCMemoryInfo.GenerationInfo"/> is a <see cref="ReadOnlySpan{T}"/> over a ref
        /// struct, so it has to be copied out rather than projected with LINQ.
        /// </para>
        /// </remarks>
        private static List<object> ReadGenerations(GCMemoryInfo info)
        {
            var generations = new List<object>(info.GenerationInfo.Length);

            for (var index = 0; index < info.GenerationInfo.Length; index++)
            {
                var generation = info.GenerationInfo[index];

                generations.Add(new
                {
                    Name = index < _generationNames.Length
                        ? _generationNames[index]
                        : index.ToString(CultureInfo.InvariantCulture),
                    SizeBeforeMb = Round(generation.SizeBeforeBytes),
                    SizeAfterMb = Round(generation.SizeAfterBytes),
                    FragmentationBeforeMb = Round(generation.FragmentationBeforeBytes),
                    FragmentationAfterMb = Round(generation.FragmentationAfterBytes),
                });
            }

            return generations;
        }
    }
}
