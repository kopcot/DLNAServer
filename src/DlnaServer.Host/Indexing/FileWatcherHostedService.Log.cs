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
    }
}
