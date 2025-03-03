namespace DLNAServer.Controllers.Manage
{
    public partial class ManageController
    {
        [LoggerMessage(1, LogLevel.Information, "Start refreshing info for chunk {indexChunk} of {totalChunks}, total files = {fileCount}")]
        partial void InformationStartRefreshingInfoChunk(int indexChunk, int totalChunks, long fileCount);
        [LoggerMessage(2, LogLevel.Debug, "Start clearing info for chunk {indexChunk} of {totalChunks}, total files = {fileCount}")]
        partial void DebugStartClearigInfoChunk(int indexChunk, int totalChunks, long fileCount);
        [LoggerMessage(3, LogLevel.Debug, "Start recreating info for chunk {indexChunk} of {totalChunks}, total files = {fileCount}")]
        partial void DebugStartRecreatingInfoChunk(int indexChunk, int totalChunks, long fileCount);
        [LoggerMessage(4, LogLevel.Debug, "Clearing info done for chunk {indexChunk} of {totalChunks}")]
        partial void DebugDoneClearingInfoChunk(int indexChunk, int totalChunks);
        [LoggerMessage(5, LogLevel.Debug, "Recreating info done for chunk {indexChunk} of {totalChunks}")]
        partial void DebugDoneRecreatingInfoChunk(int indexChunk, int totalChunks);
        [LoggerMessage(6, LogLevel.Information, "Recreate info done")]
        partial void InformationDoneRecreatingInfo();

        private static readonly Func<ILogger, IDisposable?> _logScopeRecreatingFilesInfo =
            LoggerMessage.DefineScope(
                "Recreating all files info");
        public static IDisposable? ScopeRecreatingFilesInfo(
            ILogger logger)
        {
            return _logScopeRecreatingFilesInfo(logger);
        }

        private static readonly Func<ILogger, IDisposable?> _logScopeClearingInfoChunk =
            LoggerMessage.DefineScope(
                "Clearing info - chunk");
        public static IDisposable? ScopeClearingInfoChunk(
            ILogger logger)
        {
            return _logScopeClearingInfoChunk(logger);
        }

        private static readonly Func<ILogger, IDisposable?> _logScopeRecreatingFilesInfoChunk =
            LoggerMessage.DefineScope(
                "Recreating files info - chunk");
        public static IDisposable? ScopeRecreatingFilesInfoChunk(
            ILogger logger)
        {
            return _logScopeRecreatingFilesInfoChunk(logger);
        }
    }
}
