namespace DLNAServer.Features.FileWatcher
{
    public partial class FileWatcherHandler
    {
        [LoggerMessage(1, LogLevel.Warning, "Path is Already watching - '{pathToWatch}'")]
        partial void WarningPathAlreadyWatching(string pathToWatch);
        [LoggerMessage(2, LogLevel.Warning, "Directory not exists: {sourceFolder}")]
        partial void WarningDirectoryNotExists(string sourceFolder);
        [LoggerMessage(3, LogLevel.Debug, "Started watching path - '{pathToWatch}'")]
        partial void DebugStartedWatchingPath(string pathToWatch);
        [LoggerMessage(4, LogLevel.Debug, "Wait for start handling event '{changeType}' for '{fullPath}'\nEventID: {guid}")]
        partial void DebugEventWaitForStart(WatcherChangeTypes changeType, string fullPath, Guid guid);
        [LoggerMessage(5, LogLevel.Debug, "Started handling event '{changeType}' for '{fullPath}'\nEventID: {guid}")]
        partial void DebugEventStarted(WatcherChangeTypes changeType, string fullPath, Guid guid);
        [LoggerMessage(6, LogLevel.Debug, "Done handling event '{changeType}' for '{fullPath}'\nEventID: {guid}")]
        partial void DebugEventDone(WatcherChangeTypes changeType, string fullPath, Guid guid);
        [LoggerMessage(7, LogLevel.Debug, "Event '{changeType}' filtered out by thumbnail subfolder for '{fullPath}'")]
        partial void DebugEventFilteredForThumbnailSubfolder(WatcherChangeTypes changeType, string fullPath);
        [LoggerMessage(8, LogLevel.Debug, "Event '{changeType}' filtered out by extension or by not a directory for '{fullPath}'")]
        partial void DebugEventFilteredForExtensionOrNotDirectory(WatcherChangeTypes changeType, string fullPath);
        [LoggerMessage(9, LogLevel.Debug, "Event '{changeType}' filtered out by exclude directories for '{fullPath}'")]
        partial void DebugEventFilteredForExcludeDirectories(WatcherChangeTypes changeType, string fullPath);
        [LoggerMessage(10, LogLevel.Warning, "Semaphore was already released for {fullPath}\n{exceptionMessage}\n{innerExceptionStackTrace}")]
        partial void WarningSemaphoreAlreadyReleased(string fullPath, string? exceptionMessage, string? innerExceptionStackTrace);
        [LoggerMessage(11, LogLevel.Warning, "Error in semaphore for {fullPath}\n{exceptionMessage}\n{innerExceptionStackTrace}")]
        partial void WarningSemaphoreInError(string fullPath, string? exceptionMessage, string? innerExceptionStackTrace);
        [LoggerMessage(12, LogLevel.Debug, "Semaphore release event '{changeType}' for {fullPath}")]
        partial void DebugSemaphoreReleased(WatcherChangeTypes changeType, string fullPath);




    }
}
