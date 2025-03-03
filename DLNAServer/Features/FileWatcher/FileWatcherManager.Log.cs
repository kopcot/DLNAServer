namespace DLNAServer.Features.FileWatcher
{
    public partial class FileWatcherManager
    {
        [LoggerMessage(1, LogLevel.Information, "Started watching files at source folders")]
        partial void InformationStartedWatchingSourceFolders();
        [LoggerMessage(2, LogLevel.Warning, "File event '{eventAction}'. File not exists '{fileFullPath}'")]
        partial void WarningFileNotExists(WatcherChangeTypes eventAction, string fileFullPath);
        [LoggerMessage(3, LogLevel.Debug, "File event '{eventAction}'. Started adding record to database, record exists already in database. File '{fileFullPath}'")]
        partial void DebugAddToDatabase(WatcherChangeTypes eventAction, string fileFullPath);
        [LoggerMessage(4, LogLevel.Debug, "File event '{eventAction}'. Record exists already in database. File '{fileFullPath}'")]
        partial void DebugExistsInDatabase(WatcherChangeTypes eventAction, string fileFullPath);
        [LoggerMessage(5, LogLevel.Warning, "File event '{eventAction}'. File record not created in database! File path: '{fileFullPath}")]
        partial void WarningFileRecordNotCreated(WatcherChangeTypes eventAction, string fileFullPath);
        [LoggerMessage(6, LogLevel.Warning, "File event '{eventAction}'. Multiple file record created in database! File path: '{fileFullPath}")]
        partial void WarningFileRecordMultiple(WatcherChangeTypes eventAction, string fileFullPath);
        [LoggerMessage(7, LogLevel.Debug, "File event '{eventAction}'. File remove - '{fileFullPath}'")]
        partial void DebugFileRemove(WatcherChangeTypes eventAction, string fileFullPath);
        [LoggerMessage(8, LogLevel.Debug, "File event '{eventAction}'. File remove done - '{fileFullPath}'")]
        partial void DebugFileRemoveDone(WatcherChangeTypes eventAction, string fileFullPath);
        [LoggerMessage(9, LogLevel.Warning, "Directory event '{eventAction}'. Directory not exists '{DirectoryFullPath}'")]
        partial void WarningDirectoryNotExists(WatcherChangeTypes eventAction, string DirectoryFullPath);
        [LoggerMessage(10, LogLevel.Debug, "Event '{eventAction}' started. Event id: {guid}, Event timestamp: {eventTimestamp}")]
        partial void DebugEventStarted(WatcherChangeTypes eventAction, Guid guid, DateTime eventTimestamp);
        [LoggerMessage(11, LogLevel.Debug, "Event '{eventAction}' successful. Event id: {guid}, Event timestamp: {eventTimestamp}")]
        partial void DebugEventSuccessful(WatcherChangeTypes eventAction, Guid guid, DateTime eventTimestamp);
        [LoggerMessage(12, LogLevel.Warning, "Event '{eventAction}' failed. Event id: {guid}, Event timestamp: {eventTimestamp}")]
        partial void WarningEventFailed(WatcherChangeTypes eventAction, Guid guid, DateTime eventTimestamp);
        [LoggerMessage(13, LogLevel.Warning, "Event '{eventAction}'. Undefined DlnaMime for '{extension}' for file {fileFullPath}")]
        partial void WarningFileExtensionUndefined(WatcherChangeTypes eventAction, string extension, string fileFullPath);



    }
}
