namespace DlnaServer.Host.Indexing
{
    internal sealed partial class LibraryIndexer
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Library scan started over {SourceFolderCount} source folder(s)")]
        private partial void LogScanStarted(int sourceFolderCount);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "Library scan finished: +{FilesAdded} added, ~{FilesUpdated} changed, "
                + "-{FilesRemoved} file(s) and -{DirectoriesRemoved} directory(ies) removed; "
                + "index now holds {TotalFiles} file(s) in {TotalDirectories} directory(ies)")]
        private partial void LogScanFinished(
            int filesAdded,
            int filesUpdated,
            int filesRemoved,
            int directoriesRemoved,
            int totalFiles,
            int totalDirectories);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Skipped '{FilePath}': its directory '{DirectoryPath}' is not indexed")]
        private partial void LogParentDirectoryMissing(string filePath, string directoryPath);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "'{FilePath}' changed on disk and will be reprocessed")]
        private partial void LogFileChanged(string filePath);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Information,
            Message = "'{DirectoryPath}' re-rooted to match configuration; source folder: {IsSourceRoot}")]
        private partial void LogDirectoryReRooted(string directoryPath, bool isSourceRoot);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Warning,
            Message = "Reconciliation was skipped and nothing was removed from the index, because these "
                + "source folder(s) cannot be read: {UnusableFolders}. Reconciling now would delete every "
                + "indexed row under them. Fix the folder(s); the next scan reconciles normally")]
        private partial void LogReconcileSkipped(string unusableFolders);

        [LoggerMessage(
            EventId = 15,
            Level = LogLevel.Warning,
            Message = "Reconciliation stopped part-way and removes nothing more this pass, because these "
                + "source folder(s) became unreadable while it ran: {UnusableFolders}. Rows already "
                + "reconciled stay as they are; the next scan reconciles normally once the folder(s) are back")]
        private partial void LogReconcileHalted(string unusableFolders);

        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Warning,
            Message = "'{FilePath}' could not be read, so its row was kept rather than removed. The file "
                + "may be gone or its folder may merely be unreadable, and the two are not the same "
                + "thing: only a definite absence is allowed to delete an indexed row")]
        private partial void LogFileAbsenceUnknown(string filePath);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Warning,
            Message = "Stopped pruning empty folders after {PassCount} passes with removals still "
                + "happening. One pass collapses one folder level, so either the library is nested past "
                + "that depth or a folder's parent chain forms a cycle. The next scan continues from here")]
        private partial void LogPruneIncomplete(int passCount);

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Information,
            Message = "First fill of source folder(s) {Folders} - indexed dates come from the filesystem, "
                + "so Recently added stays meaningful across a rebuild.")]
        private partial void LogFirstFill(string folders);

        [LoggerMessage(
            EventId = 10,
            Level = LogLevel.Information,
            Message = "'{OldFullPath}' has been renamed to '{NewFullPath}' - the same library entry "
                + "follows it, keeping its identifier, its metadata and the date it was added")]
        private partial void LogFileRenamed(string oldFullPath, string newFullPath);

        [LoggerMessage(
            EventId = 11,
            Level = LogLevel.Debug,
            Message = "The preview '{FilePath}' left behind by a moved file could not be deleted "
                + "({Reason})")]
        private partial void LogThumbnailCleanupFailed(string filePath, string reason);

        [LoggerMessage(
            EventId = 12,
            Level = LogLevel.Information,
            Message = "'{OldFullPath}' has moved to '{NewFullPath}' - the same library entry follows it, "
                + "keeping its identifier, its metadata and the date it was added")]
        private partial void LogFileMoved(string oldFullPath, string newFullPath);

        [LoggerMessage(
            EventId = 13,
            Level = LogLevel.Warning,
            Message = "No source folders are configured, so {Folders} is being served as a fallback. "
                + "{KeptCount} indexed folder(s) outside it were kept rather than removed - a missing or "
                + "emptied config.json is not a decision to stop sharing them. Set "
                + "Dlna.Library.SourceFolders to reconcile normally")]
        private partial void LogFallbackKeptUncoveredFolders(string folders, int keptCount);

        [LoggerMessage(
            EventId = 14,
            Level = LogLevel.Information,
            Message = "Subtitles: {LinkedCount} file(s) newly linked to their media, {UnlinkedCount} link(s) dropped")]
        private partial void LogSubtitlesLinked(int linkedCount, int unlinkedCount);
    }
}
