using DlnaServer.Core.Contracts;
using DlnaServer.Core.Delivery;
using DlnaServer.Host.Delivery.Prefetch;

namespace DlnaServer.Host.Delivery
{
    /// <summary>
    /// Decides where one media file's bytes come from, and keeps the byte cache filling.
    /// </summary>
    /// <remarks>
    /// Shared by the renderer-facing <c>/fileserver</c> and the admin UI's <c>/admin/media</c> so the two
    /// cannot drift: a film watched through the admin page is the same workload as one watched on a
    /// television, and it should get the same treatment - served from memory when it is there, and queued
    /// to be read into memory when it is not, so the disc is not woken a second time for it.
    /// <para>
    /// The admin controller originally read the cache but never filled it, on the theory that an operator
    /// glancing at a file should not disturb what a renderer is streaming. That was the wrong call: the
    /// two paths deliver the same content, and having them behave differently makes the acoustic goal
    /// depend on which door the request came through.
    /// </para>
    /// </remarks>
    internal sealed class MediaContentResolver : IMediaContentResolver
    {
        private readonly IServedFileCache _cache;
        private readonly IMediaCacheBacklog _backlog;

        public MediaContentResolver(IServedFileCache cache, IMediaCacheBacklog backlog)
        {
            _cache = cache;
            _backlog = backlog;
        }

        public MediaContentSource Resolve(MediaFileDto file)
        {
            ArgumentNullException.ThrowIfNull(file);

            // A file that already failed to be read into memory is never retried: the flag is cleared
            // when its content changes, which is the only thing that makes another attempt worthwhile.
            if (file.IsExcludedFromCache)
            {
                return MediaContentSource.FromDisc();
            }

            if (_cache.TryGet(file.FullPath, out var cached))
            {
                return MediaContentSource.FromCache(cached);
            }

            // Compared live rather than recorded, because the limit is the one reason a file cannot be
            // cached that the operator can change: raising MaxFileSizeInMegabytes has to be enough to
            // make this file cacheable on the next request, with no rescan and nothing to clear.
            if (file.SizeInBytes > _cache.MaxFileSizeInBytes)
            {
                return MediaContentSource.FromDisc();
            }

            // Queued, never awaited. The first request touches the platter either way, so blocking on the
            // read would add latency and save nothing; every later request is served from memory.
            _ = _backlog.TryEnqueue(new MediaCacheRequest(file.PublicId, file.FullPath));

            return MediaContentSource.FromDisc();
        }
    }
}
