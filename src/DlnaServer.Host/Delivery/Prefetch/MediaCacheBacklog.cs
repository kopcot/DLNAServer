using System.Collections.Concurrent;
using System.Threading.Channels;

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

        private readonly ConcurrentDictionary<string, byte> _waitingPaths = new(StringComparer.Ordinal);

        public ChannelReader<MediaCacheRequest> Reader => _channel.Reader;

        public bool TryEnqueue(MediaCacheRequest request)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.FilePath);

            // A renderer issues many range requests for one file, so without this the same file would be
            // added - and read - dozens of times over a single playback.
            if (!_waitingPaths.TryAdd(request.FilePath, value: 0))
            {
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

            _ = _waitingPaths.TryRemove(filePath, out _);
        }
    }
}
