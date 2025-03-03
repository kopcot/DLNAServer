using DLNAServer.Common;
using DLNAServer.Configuration;
using DLNAServer.Features.FileWatcher.Interfaces;
using DLNAServer.Helpers.Logger;
using System.Collections.Concurrent;

namespace DLNAServer.Features.FileWatcher
{
    public partial class FileWatcherHandler : IFileWatcherHandler
    {
        private readonly ILogger<FileWatcherHandler> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ServerConfig _serverConfig;
        private readonly static ConcurrentDictionary<string, FileSystemWatcher> _fileSystemWatchers = new();
        private readonly static ConcurrentDictionary<string, (int Count, SemaphoreSlim Semaphore)> _fileEventsInProgress = new();
        public FileWatcherHandler(
            ILogger<FileWatcherHandler> logger,
            IServiceScopeFactory serviceScopeFactory,
            ServerConfig serverConfig)
        {
            _logger = logger;
            _serverConfig = serverConfig;
            _serviceScopeFactory = serviceScopeFactory;
        }
        public void WatchPath(string pathToWatch)
        {
            if (_fileSystemWatchers.ContainsKey(pathToWatch))
            {
                WarningPathAlreadyWatching(pathToWatch);
                return;
            }

            if (!Directory.Exists(pathToWatch))
            {
                WarningDirectoryNotExists(pathToWatch);
                return;
            }

            var watcher = new FileSystemWatcher(pathToWatch)
            {
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    //| NotifyFilters.Attributes 
                    //| NotifyFilters.Size
                    | NotifyFilters.LastWrite
                    //| NotifyFilters.LastAccess 
                    //| NotifyFilters.CreationTime
                    //| NotifyFilters.Security
                    ,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true,
                InternalBufferSize = 1_024_000,
            };

            // cannot be done, for Linux it is different file if it is .jpg, .JPG or .Jpg
            //ServerConfig.Extensions.ToList().ForEach(ex => watcher.Filters.Add("*" + ex.Key)); 

            watcher.Created += async (sender, args) => await ExecuteEventHandlerAsync(pathToWatch, args.FullPath, null, WatcherChangeTypes.Created);
            watcher.Changed += async (sender, args) => await ExecuteEventHandlerAsync(pathToWatch, args.FullPath, null, WatcherChangeTypes.Changed);
            watcher.Renamed += async (sender, args) => await ExecuteEventHandlerAsync(pathToWatch, args.FullPath, args.OldFullPath, WatcherChangeTypes.Renamed);
            watcher.Deleted += async (sender, args) => await ExecuteEventHandlerAsync(pathToWatch, args.FullPath, null, WatcherChangeTypes.Deleted);

            _ = _fileSystemWatchers.TryAdd(pathToWatch, watcher);

            DebugStartedWatchingPath(pathToWatch);
        }
        public void EnableRaisingEvents(bool enable)
        {
            foreach (var watcher in _fileSystemWatchers.ToList())
            {
                watcher.Value.EnableRaisingEvents = enable;
            }
        }

        private static void UnwatchPath(string pathToWatch)
        {
            if (_fileSystemWatchers.TryRemove(pathToWatch, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }
        private bool ShouldExcludeByThumbnailPath(string fullPath)
        {
            return fullPath.Contains(_serverConfig.SubFolderForThumbnail, StringComparison.InvariantCultureIgnoreCase);
        }
        private bool ShouldExcludeByExcludeFoldersPath(string fullPath)
        {
            return _serverConfig.ExcludeFolders.Any(exclude => fullPath.Contains(exclude, StringComparison.InvariantCultureIgnoreCase));
        }
        private bool IsFileExtensionMatch(string fullPath)
        {
            string fileExtension = new FileInfo(fullPath).Extension;
            return _serverConfig.MediaFileExtensions.Any(extension => fileExtension.EndsWith(extension.Key, StringComparison.InvariantCultureIgnoreCase));
        }
        private static bool IsDirectory(string fullPath)
        {
            return Directory.Exists(fullPath);
        }
        private async Task ExecuteEventHandlerAsync(
            string watchedPath,
            string fullPath,
            string? fullPathOld,
            WatcherChangeTypes changeType
            )
        {
            DateTime eventTimestamp = DateTime.Now;
            SemaphoreSlim? fileLock = null;

            if (CheckPathForExclude(changeType, fullPath, eventTimestamp))
            {
                return;
            }

            Guid guid = Guid.NewGuid();

            try
            {
                fileLock = _fileEventsInProgress.AddOrUpdate(fullPath, (1, new SemaphoreSlim(1, 1)), (key, value) => (value.Count + 1, value.Semaphore)).Semaphore;

                DebugEventWaitForStart(changeType, fullPath, guid);
                _ = await fileLock.WaitAsync(TimeSpanValues.Time30min);
                DebugEventStarted(changeType, fullPath, guid);

                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var fileWatcherManager = scope.ServiceProvider.GetRequiredService<IFileWatcherManager>();

                    if (IsFileExtensionMatch(fullPath))
                    {
                        switch (changeType)
                        {
                            case WatcherChangeTypes.Created:
                            case WatcherChangeTypes.Changed:
                                if (!ShouldExcludeByExcludeFoldersPath(fullPath))
                                {
                                    await fileWatcherManager.HandleFileCreatedChanged(fullPath, changeType, eventTimestamp);
                                }
                                break;
                            case WatcherChangeTypes.Renamed:
                                await fileWatcherManager.HandleFileRenamed(fullPath, fullPathOld!, changeType, eventTimestamp);
                                break;
                            case WatcherChangeTypes.Deleted:
                                await fileWatcherManager.HandleFileRemove(fullPath, changeType, eventTimestamp);
                                break;
                        }
                    }
                    else if (IsDirectory(fullPath))
                    {
                        switch (changeType)
                        {
                            case WatcherChangeTypes.Renamed:
                                await fileWatcherManager.HandleDirectoryRenamed(fullPath, fullPathOld!, changeType, eventTimestamp);
                                break;
                            case WatcherChangeTypes.Deleted:
                                await fileWatcherManager.HandleDirectoryRemove(fullPath, changeType, eventTimestamp);
                                break;
                        }
                    }
                }
                DebugEventDone(changeType, fullPath, guid);
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
            }
            finally
            {
                ReleaseSemaphore(changeType, fullPath);
            }
        }

        private bool CheckPathForExclude(WatcherChangeTypes changeType, string fullPath, DateTime eventTimestamp)
        {
            if (ShouldExcludeByThumbnailPath(fullPath))
            {
                DebugEventFilteredForThumbnailSubfolder(changeType, fullPath);
                return true;
            }

            //if (ShouldExcludeByExcludeFoldersPath(fullPath))
            //{
            //    DebugEventFilteredForExcludeDirectories(changeType, fullPath);
            //    return true;
            //}

            if (!IsFileExtensionMatch(fullPath) && !IsDirectory(fullPath))
            {
                DebugEventFilteredForExtensionOrNotDirectory(changeType, fullPath);
                return true;
            }

            return false;
        }
        private void ReleaseSemaphore(WatcherChangeTypes changeType, string fullPath)
        {
            try
            {
                var fileLock = _fileEventsInProgress.AddOrUpdate(fullPath, (0, new SemaphoreSlim(1, 1)), (key, value) => (value.Count - 1, value.Semaphore));

                _ = fileLock.Semaphore.Release();
                if (fileLock.Count <= 0)
                {
                    _ = _fileEventsInProgress.Remove(fullPath, out _);
                }
            }
            catch (SemaphoreFullException ex)
            {
                WarningSemaphoreAlreadyReleased(fullPath, ex.Message, ex.InnerException?.StackTrace);
            }
            catch (Exception ex)
            {
                WarningSemaphoreInError(fullPath, ex.Message, ex.InnerException?.StackTrace);
            }

            DebugSemaphoreReleased(changeType, fullPath);

        }
        public Task TerminateAsync()
        {
            foreach (var watcher in _fileSystemWatchers.ToList())
            {
                UnwatchPath(watcher.Key);
            }

            foreach (var fileLocks in _fileEventsInProgress.ToList())
            {
                try
                {
                    _ = (fileLocks.Value.Semaphore?.Release());
                }
                catch { }
            }
            _fileEventsInProgress.Clear();
            _fileSystemWatchers.Clear();

            return Task.CompletedTask;
        }
    }
}
