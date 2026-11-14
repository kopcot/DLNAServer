using DlnaServer.Core.Contracts.Watching;

namespace DlnaServer.Host.Indexing
{
    internal sealed partial class FileWatcherHostedService
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "{PathCount} watched path(s) have settled; updating the index")]
        private partial void LogApplyingChanges(int pathCount);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Error,
            Message = "Applying watched changes failed. The next change will trigger another attempt.")]
        private partial void LogUpdateFailed(Exception exception);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Error,
            Message = "The filesystem watch could not be started, so changes will not be picked up until "
                + "the server is restarted. On Linux this is usually an exhausted inotify watch limit.")]
        private partial void LogWatcherStartFailed(Exception exception);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Information,
            Message = "File system watches rebuilt (after a fault: {Faulted}). A faulted watch is dead "
                + "rather than behind, and the configured folders are hot-reloadable, so both reasons "
                + "have to move the watch.")]
        private partial void LogWatchRebuilt(bool faulted);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Error,
            Message = "A watcher tick failed; the watch continues")]
        private partial void LogTickFailed(Exception exception);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Warning,
            Message = "The file system watch faulted again straight after being rebuilt, so rebuilds now back "
                + "off from {FirstDelayMinutes} to {MaxDelayMinutes} minute(s). On Linux this is almost always "
                + "an exhausted inotify limit - raise fs.inotify.max_user_watches (and "
                + "fs.inotify.max_user_instances) on the host.")]
        private partial void LogWatchKeepsFaulting(int firstDelayMinutes, int maxDelayMinutes);

        /// <remarks>
        /// Trace, not Debug: with the application folder as the library, the log files raise one of these
        /// for every line written, so anything louder would bury the log it is meant to help read.
        /// </remarks>
        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Trace,
            Message = "Ignored a {Kind} change to '{Path}' (renamed from '{OldPath}', for a rename): the library "
                + "does not index that file type, so it cannot change what the library holds")]
        private partial void LogIgnoredChange(FileChangeKind kind, string path, string? oldPath);

        /// <remarks>
        /// Trace for the same reason as <see cref="LogIgnoredChange"/>: the database's write-ahead log changes
        /// on every write a scan makes.
        /// </remarks>
        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Trace,
            Message = "Ignored a {Kind} change to '{Path}' (renamed from '{OldPath}', for a rename): it is the "
                + "server's own database or settings file, or a backup of one, which no scan can index")]
        private partial void LogIgnoredServerFileChange(FileChangeKind kind, string path, string? oldPath);

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Information,
            Message = "Source folder(s) {Folders} are now present with something in them and watched; a full scan "
                + "is requested so the files already there are indexed")]
        private partial void LogSourceFoldersRecovered(string folders);

        [LoggerMessage(
            EventId = 10,
            Level = LogLevel.Debug,
            Message = "{Count} source folder(s) are still missing, empty or unreadable; the watch is left as it is "
                + "and checked again in a minute")]
        private partial void LogSourceFoldersStillNotReady(int count);
    }
}
