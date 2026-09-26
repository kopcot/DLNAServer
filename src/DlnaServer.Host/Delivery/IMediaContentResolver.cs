using DlnaServer.Core.Contracts;
using DlnaServer.Persistence.Repositories;

namespace DlnaServer.Host.Delivery
{
    /// <summary>
    /// Decides where a media file's bytes come from, identically for every caller.
    /// </summary>
    public interface IMediaContentResolver
    {
        /// <summary>
        /// Resolves the source, and queues a cache fill when the bytes are not held yet.
        /// </summary>
        /// <remarks>
        /// Has a side effect by design: resolving is also what schedules the read. Two callers asking
        /// about the same file therefore get the same answer <b>and</b> the same caching behaviour, which
        /// is the point of having one implementation rather than a copy per controller.
        /// </remarks>
        MediaContentSource Resolve(MediaFileDto file);

        /// <summary>
        /// Finds a thumbnail's bytes, trying each source in turn, and caches what it had to read.
        /// </summary>
        /// <remarks>
        /// The order is the served-bytes cache, then the database copy when one was stored, then the file
        /// read into the cache, then the file streamed from disc. Preferring the database over the file
        /// reproduces the reference: the row is reachable through SQLite's own page cache while the file is
        /// a separate seek. Both ports serve thumbnails through this, because the two copies of this ladder
        /// had already drifted apart once.
        /// <para>
        /// The repository is a parameter rather than a dependency: this service is a singleton and the
        /// repository is scoped to the request that asks.
        /// </para>
        /// </remarks>
        /// <returns>
        /// The source, and the payload for <see cref="ThumbnailSource.Cache"/> and
        /// <see cref="ThumbnailSource.Database"/>. Empty for <see cref="ThumbnailSource.Disc"/>, where the
        /// caller streams <see cref="ThumbnailDto.FilePath"/>, and for <see cref="ThumbnailSource.None"/>.
        /// </returns>
        ValueTask<(ThumbnailSource Source, ReadOnlyMemory<byte> Content)> ResolveThumbnailAsync(
            ThumbnailDto thumbnail,
            IMediaFileRepository files,
            CancellationToken cancellationToken = default);
    }
}
