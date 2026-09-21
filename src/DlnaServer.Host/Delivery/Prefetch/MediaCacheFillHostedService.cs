using DlnaServer.Core.Hosting;
using DlnaServer.Host.Delivery.Caching;
using DlnaServer.Persistence.Repositories;
using DlnaServer.Core.Delivery;

namespace DlnaServer.Host.Delivery.Prefetch
{
    /// <summary>
    /// Reads the media files waiting in the backlog into the served-bytes cache, off the request path.
    /// </summary>
    /// <remarks>
    /// One file at a time and no parallelism: each read is a whole file into memory, so concurrency here
    /// would multiply peak memory and contend for the same disc it exists to spare.
    /// </remarks>
    internal sealed partial class MediaCacheFillHostedService : BackgroundService
    {
        private const string SourceCache = "cache";
        private const string SourceDisc = "disc";
        private const string SourceDatabase = "database";

        private readonly IMediaCacheBacklog _backlog;
        private readonly IServedFileCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDatabaseReadySignal _readySignal;
        private readonly ILogger<MediaCacheFillHostedService> _logger;

        public MediaCacheFillHostedService(
            IMediaCacheBacklog backlog,
            IServedFileCache cache,
            IServiceScopeFactory scopeFactory,
            IDatabaseReadySignal readySignal,
            ILogger<MediaCacheFillHostedService> logger)
        {
            _backlog = backlog;
            _cache = cache;
            _scopeFactory = scopeFactory;
            _readySignal = readySignal;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // FillAsync writes IsExcludedFromCache, so this one needs a schema too.
                await _readySignal.WaitAsync(stoppingToken);

                await foreach (var request in _backlog.Reader.ReadAllAsync(stoppingToken))
                {
                    await FillAsync(request, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        private async Task FillAsync(MediaCacheRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (!_cache.IsEnabled)
                {
                    return;
                }

                var (content, source) = await LoadAsync(request, cancellationToken);

                if (!content.IsEmpty)
                {
                    LogFilled(request.FilePath, content.Length, source);
                    return;
                }

                // LoadAsync answers empty for four different reasons - too large, missing, zero-length,
                // and a caught IO or access failure - and only the last says anything about the file
                // itself. Size is explicitly NOT recorded: the per-file limit is operator-configurable,
                // so a file that does not fit today fits tomorrow once MaxFileSizeInMegabytes is raised,
                // and a flag cleared only by the content changing would outlive the limit that set it.
                // MediaContentResolver compares the size live instead, which costs nothing and needs no
                // clearing.
                // A preview is never condemned. The exclusion flag lives on the MEDIA file, and a
                // preview that could not be read means it has not been generated yet - recording that
                // against the film would send the film itself to the platter on every viewing, for a
                // thumbnail that will exist in a minute.
                if (request.ContentClass != CachedContentClass.Media || !IsUnreadable(request.FilePath))
                {
                    LogFillDeferred(request.FilePath);
                    return;
                }

                await MarkExcludedAsync(request.PublicId, cancellationToken);
                LogExcluded(request.FilePath);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Filling the cache is an optimisation - the request it was queued behind has already
                // been served from disc. Nothing here is worth stopping the drain for, and a SQLite
                // failure while recording the exclusion is the likeliest cause.
                LogFillFailed(request.FilePath, exception);
            }
            finally
            {
                _backlog.Release(request.FilePath);
            }
        }

        /// <summary>
        /// Whether the file itself refuses to be read - an IO failure or a permission denial.
        /// </summary>
        /// <remarks>
        /// The read is proven by opening the file, not inferred from the empty answer: a file that is
        /// merely too large, zero-length or already gone opens fine or does not exist, and none of those
        /// is a property of the file worth recording. Opening costs a handle on a path that only runs
        /// after a fill produced nothing, which is rare.
        /// <para>
        /// A missing file is not unreadable. Reconciliation owns that case and removes the row; marking
        /// it here would race that and condemn a row about to disappear.
        /// </para>
        /// </remarks>
        private static bool IsUnreadable(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                using var probe = File.OpenRead(filePath);

                return false;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return true;
            }
        }

        private async Task MarkExcludedAsync(Guid publicId, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();

            await files.MarkExcludedFromCacheAsync(publicId, cancellationToken);
        }

        /// <summary>
        /// Reads one payload, preferring the database's copy of a preview over the image on disc.
        /// </summary>
        /// <remarks>
        /// <see cref="IServedFileCache.LoadAsync"/> only ever reads the filesystem. With
        /// <c>Thumbnails.StoreInDatabase</c> on - which is the default - the bytes are already in SQLite,
        /// so warming from disc woke the platter for something <c>FileServerController.GetThumbnail</c>
        /// would then have taken from the database, which is the opposite of what this prefetch exists
        /// for. The order below is that method's order, so the warm and the serve cannot disagree about
        /// where a preview comes from.
        /// </remarks>
        private async Task<(ReadOnlyMemory<byte> Content, string Source)> LoadAsync(
            MediaCacheRequest request,
            CancellationToken cancellationToken)
        {
            if (request.ContentClass != CachedContentClass.Thumbnail)
            {
                // Only a preview is ever held anywhere but the filesystem, so nothing else has a source
                // to choose between.
                return (await _cache.LoadAsync(request.FilePath, request.ContentClass, cancellationToken), SourceDisc);
            }

            // Tested before the query rather than left to LoadAsync: a folder browsed a second time would
            // otherwise re-read the blob for a preview already held.
            if (_cache.TryGet(request.FilePath, out var cached))
            {
                return (cached, SourceCache);
            }

            var stored = await ReadStoredThumbnailAsync(request.PublicId, cancellationToken);

            if (stored is { Length: > 0 })
            {
                _cache.Store(request.FilePath, CachedContentClass.Thumbnail, stored);

                return (stored, SourceDatabase);
            }

            // Either StoreInDatabase was off when this preview was generated, or the row is gone. The
            // image beside the media is then the only copy, and the derived path is where it lives.
            var fromDisc = await _cache.LoadAsync(request.FilePath, request.ContentClass, cancellationToken);

            return (fromDisc, SourceDisc);
        }

        private async Task<byte[]?> ReadStoredThumbnailAsync(
            Guid thumbnailPublicId,
            CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();

            return await files.GetThumbnailContentAsync(thumbnailPublicId, cancellationToken);
        }
    }
}
