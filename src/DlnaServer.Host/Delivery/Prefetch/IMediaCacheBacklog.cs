using System.Threading.Channels;

namespace DlnaServer.Host.Delivery.Prefetch
{
    /// <summary>
    /// Carries media files that are being served from the disc now and should be in memory next time.
    /// </summary>
    /// <remarks>
    /// The backlog exists so that filling the cache never delays a response. The first request for a file
    /// is served straight from the disc while the read happens behind it - waiting for the cache to fill
    /// would add latency and gain nothing, because that first request has to touch the platter either way.
    /// </remarks>
    public interface IMediaCacheBacklog
    {
        /// <summary>
        /// Files waiting to be read, drained by <see cref="MediaCacheFillHostedService"/> one at a time.
        /// </summary>
        ChannelReader<MediaCacheRequest> Reader { get; }

        /// <summary>
        /// Adds a file, unless it is already waiting or the backlog is full.
        /// </summary>
        /// <remarks>
        /// Returning false is a normal outcome and never an error: caching is an optimisation, so a
        /// dropped request simply means the next play of that file reads the disc again. The reference
        /// used an unbounded channel here, which let a burst of requests accumulate unbounded memory.
        /// </remarks>
        bool TryEnqueue(MediaCacheRequest request);

        /// <summary>
        /// Releases a path once it has been dealt with, so a later request can add it again.
        /// </summary>
        void Release(string filePath);
    }
}
