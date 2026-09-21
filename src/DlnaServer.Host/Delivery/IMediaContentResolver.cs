using DlnaServer.Core.Contracts;

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
    }
}
