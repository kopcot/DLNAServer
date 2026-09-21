namespace DlnaServer.Host.Controllers
{
    public sealed partial class ManageController
    {
        /// <summary>
        /// Warning, not Information: the endpoint is unauthenticated, so the requester's address is the
        /// only record of who stopped the server.
        /// </summary>
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "Shutdown requested by {RemoteAddress}; stopping")]
        private partial void LogStopRequested(string? remoteAddress);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "Served-bytes cache cleared by {RemoteAddress}; {EntryCount} entr(ies) dropped "
                + "and a compacting collection forced")]
        private partial void LogFileCacheCleared(int entryCount, string? remoteAddress);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "{Operation} requested by {RemoteAddress}; {FilesAffected} file(s) affected")]
        private partial void LogMaintenance(string operation, int filesAffected, string? remoteAddress);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "Requests blocked for {Hours} hour(s) by {RemoteAddress}; /manage stays reachable")]
        private partial void LogBlocked(int hours, string? remoteAddress);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Warning,
            Message = "Request block lifted by {RemoteAddress}")]
        private partial void LogUnblocked(string? remoteAddress);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Warning,
            Message = "Restart requested by {RemoteAddress}; the host will be rebuilt in place")]
        private partial void LogRestartRequested(string? remoteAddress);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Error,
            Message = "{Operation} committed only part of its work: {ThumbnailsCleared} thumbnail "
                + "record(s) were cleared and the metadata clear then failed. The thumbnails regenerate "
                + "on their own; the metadata does not, so the operation needs running again.")]
        private partial void LogPartialMaintenance(
            string operation,
            int thumbnailsCleared,
            Exception exception);
    }
}
