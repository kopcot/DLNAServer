namespace DlnaServer.Host.Indexing
{
    internal sealed partial class LibraryIndexHostedService
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Error,
            Message = "Library scan failed. Whatever was already indexed remains available.")]
        private partial void LogScanFailed(Exception exception);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "Query planner statistics refreshed after an indexing pass that changed the library.")]
        private partial void LogStatisticsRefreshed();

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Query-planner statistics could not be refreshed. Browsing is unaffected; the "
                + "next pass that changes something tries again.")]
        private partial void LogStatisticsRefreshFailed(Exception exception);
    }
}
