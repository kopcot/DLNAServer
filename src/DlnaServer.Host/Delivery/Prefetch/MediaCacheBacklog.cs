using System.Collections.Concurrent;
using System.Threading.Channels;
using DlnaServer.Core.Delivery;

namespace DlnaServer.Host.Delivery.Prefetch
{
    /// <inheritdoc cref="IMediaCacheBacklog"/>
    internal sealed class MediaCacheBacklog : IMediaCacheBacklog
    {
        /// <summary>
        /// Files that may be waiting at once. Small on purpose: each one is a whole file read into
        /// memory, so a longer backlog would describe more work than the budget can hold anyway.
        /// </summary>
        private const int Capacity = 32;

        /// <summary>
        /// How many of those slots previews may hold at once, waiting or being read.
        /// </summary>
        /// <remarks>
        /// Browse warms a whole page of previews at a time, so without a cap one page filled the backlog
        /// and the film a television asked for next was refused - the one read the cache exists for. The
        /// remaining slots are left to media.
        /// </remarks>
        private const int MaxWaitingThumbnails = 24;

        /// <remarks>
        /// <see cref="BoundedChannelFullMode.Wait"/> is what makes the writer report a full backlog:
        /// <c>TryWrite</c> returns false rather than accepting the item. The dropping
        /// modes return <b>true</b> while discarding it, which would leave the path marked as waiting for
        /// a read that never happens - and that file could then never be cached again.
        /// </remarks>
        private readonly Channel<MediaCacheRequest> _channel = Channel.CreateBounded<MediaCacheRequest>(
            new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
            });

        // Valued by content class, so releasing a path knows whether it gives a preview slot back.
        private readonly ConcurrentDictionary<string, CachedContentClass> _waitingPaths = new(StringComparer.Ordinal);

        private int _waitingThumbnails;

        public ChannelReader<MediaCacheRequest> Reader => _channel.Reader;

        public bool TryEnqueue(MediaCacheRequest request)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.FilePath);

            var isThumbnail = request.ContentClass == CachedContentClass.Thumbnail;

            // Reserved before the path is claimed, so two concurrent pages cannot both slip past the cap.
            if (isThumbnail && Interlocked.Increment(ref _waitingThumbnails) > MaxWaitingThumbnails)
            {
                _ = Interlocked.Decrement(ref _waitingThumbnails);
                return false;
            }

            // A renderer issues many range requests for one file, so without this the same file would be
            // added - and read - dozens of times over a single playback.
            if (!_waitingPaths.TryAdd(request.FilePath, request.ContentClass))
            {
                if (isThumbnail)
                {
                    _ = Interlocked.Decrement(ref _waitingThumbnails);
                }

                return false;
            }

            if (_channel.Writer.TryWrite(request))
            {
                return true;
            }

            Release(request.FilePath);
            return false;
        }

        public void Release(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (_waitingPaths.TryRemove(filePath, out var contentClass) && contentClass == CachedContentClass.Thumbnail)
            {
                _ = Interlocked.Decrement(ref _waitingThumbnails);
            }
        }
    }
}
