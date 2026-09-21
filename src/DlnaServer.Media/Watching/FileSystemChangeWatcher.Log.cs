namespace DlnaServer.Media.Watching
{
    internal sealed partial class FileSystemChangeWatcher
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Watching '{SourceFolder}' for changes")]
        private partial void LogWatching(string sourceFolder);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "File system watcher for '{SourceFolder}' reported an error ({Reason}). "
                + "Events may have been lost.")]
        private partial void LogWatcherError(string sourceFolder, string reason);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Change events were lost. A full rescan has been requested to resynchronise the index.")]
        private partial void LogResyncRequested();

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "The watch on '{SourceFolder}' faulted and will be rebuilt. On Linux an inotify "
                + "overflow tears the watcher down, and without rebuilding it that folder stays deaf for "
                + "the life of the process.")]
        private partial void LogRestartRequested(string sourceFolder);

        /// <remarks>
        /// Warning rather than Information: this folder contributes nothing until it comes back, and the
        /// skip used to be a bare <c>continue</c> with no line at all - so a server that started before
        /// a share mounted looked healthy while half the library was unwatched.
        /// </remarks>
        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Warning,
            Message = "'{SourceFolder}' is not there, so no watch was attached to it and changes under it "
                + "will not be noticed. It is retried while it stays missing.")]
        private partial void LogFolderNotAttached(string sourceFolder);
    }
}
