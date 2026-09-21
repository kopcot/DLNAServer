namespace DlnaServer.Core.Diagnostics
{
    /// <summary>
    /// Which parts of media processing this deployment can actually perform.
    /// </summary>
    /// <remarks>
    /// Exists because a missing capability was invisible. ffmpeg is optional - indexing, browsing and
    /// streaming all work without it - so its absence is not an error and was reported as a single
    /// startup warning. On the live server that one line stood for a 25,504-file library with no video
    /// metadata at all: no durations, no resolutions, no codecs, no audio or subtitle streams, and
    /// therefore no language filters on the search page, which is how it was eventually noticed.
    /// <para>
    /// In <c>DlnaServer.Core</c> rather than beside the provisioner so the admin UI can read it: that
    /// project references only Core and Persistence.
    /// </para>
    /// </remarks>
    public interface IMediaCapabilities
    {
        /// <summary>
        /// Whether ffmpeg and ffprobe were found, or <see langword="null"/> while nothing has needed them
        /// yet.
        /// </summary>
        /// <remarks>
        /// Three states, not two, and the third is not a technicality: availability is resolved on first
        /// use, so a server that has not yet processed a file knows nothing, and reporting that as
        /// "unavailable" would raise a false alarm on every fresh start. Reading this never triggers the
        /// resolution, so it cannot start a download as a side effect of rendering a page.
        /// </remarks>
        bool? IsVideoProcessingAvailable { get; }
    }
}
