namespace DlnaServer.Host.Upnp.Control
{
    internal sealed partial class ContentDirectoryService
    {
        /// <summary>
        /// Mirrors the reference's browse telemetry so response times can be compared like for like
        /// against the production baseline (p50 16.8 ms, p90 123.3 ms).
        /// </summary>
        /// <remarks>
        /// The DIDL <c>Filter</c> is deliberately not here. It is a per-device constant - an LG sends the
        /// same 231-character string on every call - so at Information it was pure repetition: measured
        /// over a 27-minute session on 2026-09-04, 831 identical copies of it were <b>47% of the entire
        /// log file</b>, on a NAS where the log is a size-capped rolling file and every wasted byte is
        /// retention lost. It moved to <see cref="LogBrowseFilter"/>, which is what
        /// <c>Dlna.Server.DebugMode</c> is for.
        /// </remarks>
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Browse from {RemoteAddress} ObjectID: {ObjectId}, Flag: {BrowseFlag}, "
                + "Starting index: {StartingIndex}, RequestedCount: {RequestedCount}, "
                + "Items: {NumberReturned} from {TotalMatches}, Duration (ms): {DurationMilliseconds:F2}")]
        private partial void LogBrowseCompleted(
            string? remoteAddress,
            string objectId,
            string browseFlag,
            int startingIndex,
            int requestedCount,
            uint numberReturned,
            uint totalMatches,
            double durationMilliseconds);

        /// <summary>
        /// Which DIDL-Lite fields the renderer asked for, which is what explains an element it then
        /// ignores.
        /// </summary>
        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Browse from {RemoteAddress} asked for Filter: {Filter}")]
        private partial void LogBrowseFilter(string? remoteAddress, string filter);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "X_SetBookmark discarded for object {ObjectId} at {PositionInSeconds} s; "
                + "this server stores no playback state")]
        private partial void LogBookmarkDiscarded(string objectId, int positionInSeconds);
    }
}
