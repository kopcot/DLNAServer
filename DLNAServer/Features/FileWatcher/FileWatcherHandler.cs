using DLNAServer.Configuration;
using DLNAServer.Features.FileWatcher.Interfaces;
using System.Collections.Concurrent;

namespace DLNAServer.Features.FileWatcher
{
    public class FileWatcherHandler : IFileWatcherHandler
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
                _logger.LogWarning($"{DateTime.Now} - Already watching this path: {pathToWatch}");
                return;
            }

            var directory = new DirectoryInfo(pathToWatch);
            if (!directory.Exists)
            {
                _logger.LogWarning($"{DateTime.Now} - Directory not exists: {pathToWatch}");
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

            watcher.Created += async (sender, args) => await ExecuteEventHandlerAsyncV2(pathToWatch, args.FullPath, null, WatcherChangeTypes.Created);
            watcher.Changed += async (sender, args) => await ExecuteEventHandlerAsyncV2(pathToWatch, args.FullPath, null, WatcherChangeTypes.Changed);
            watcher.Renamed += async (sender, args) => await ExecuteEventHandlerAsyncV2(pathToWatch, args.FullPath, args.OldFullPath, WatcherChangeTypes.Renamed);
            watcher.Deleted += async (sender, args) => await ExecuteEventHandlerAsyncV2(pathToWatch, args.FullPath, null, WatcherChangeTypes.Deleted);

            _ = _fileSystemWatchers.TryAdd(pathToWatch, watcher);

            _logger.LogDebug($"Started watching path: {pathToWatch}");
        }

        public static void UnwatchPath(string pathToWatch)
        {
            if (_fileSystemWatchers.TryRemove(pathToWatch, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }
        private bool ShouldExcludeByPath(string fullPath)
        {
            return _serverConfig.ExcludeFolders.Any(exclude => fullPath.Contains(exclude, StringComparison.InvariantCultureIgnoreCase));
        }
        private bool IsFileExtensionMatch(string fullPath)
        {
            return _serverConfig.MediaFileExtensions.Any(extension => fullPath.EndsWith(extension.Key, StringComparison.InvariantCultureIgnoreCase));
        }
        private static bool IsDirectory(string fullPath)
        {
            return new DirectoryInfo(fullPath).Exists;
        }
        private async Task ExecuteEventHandlerAsyncV2(
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

                _logger.LogDebug($"{DateTime.Now} - {guid} - Wait for start handling event {changeType} for {fullPath}");
                await fileLock.WaitAsync();
                _logger.LogDebug($"{DateTime.Now} - {guid} - Started handling event {changeType} for {fullPath}");

                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var fileWatcherManager = scope.ServiceProvider.GetRequiredService<IFileWatcherManager>();

                    if (IsFileExtensionMatch(fullPath))
                    {
                        if (changeType == WatcherChangeTypes.Created ||
                            changeType == WatcherChangeTypes.Changed)
                        {
                            if (!ShouldExcludeByPath(fullPath))
                            {
                                await fileWatcherManager.HandleFileCreatedChanged(fullPath, changeType, eventTimestamp);
                            }
                        }
                        else if (changeType == WatcherChangeTypes.Deleted)
                        {
                            await fileWatcherManager.HandleFileRemove(fullPath, changeType, eventTimestamp);
                        }
                        else if (changeType == WatcherChangeTypes.Renamed)
                        {
                            await fileWatcherManager.HandleFileRenamed(fullPath, fullPathOld!, changeType, eventTimestamp);
                        }
                    }
                    else if (IsDirectory(fullPath))
                    {
                        if (changeType == WatcherChangeTypes.Deleted)
                        {
                            await fileWatcherManager.HandleDirectoryRemove(fullPath, changeType, eventTimestamp);
                        }
                        else if (changeType == WatcherChangeTypes.Renamed)
                        {
                            await fileWatcherManager.HandleDirectoryRenamed(fullPath, fullPathOld!, changeType, eventTimestamp);
                        }
                    }
                }
                _logger.LogDebug($"{DateTime.Now} - {guid} - Event {changeType} done for {fullPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message, [fullPath, changeType, watchedPath]);
            }
            finally
            {
                ReleaseSemaphore(changeType, fullPath);
            }
        }

        private bool CheckPathForExclude(WatcherChangeTypes changeType, string fullPath, DateTime eventTimestamp)
        {
            //if (ShouldExcludeByPath(fullPath))
            //{
            //    _logger.LogDebug($"{DateTime.Now} - Event '{changeType}' filtered out by watching path for file {fullPath}");
            //    return true;
            //}

            if (!IsFileExtensionMatch(fullPath) && !IsDirectory(fullPath))
            {
                _logger.LogDebug($"{DateTime.Now} - Event '{changeType}' filtered out by extension or by not a directory for file {fullPath}");
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
                _logger.LogWarning($"{DateTime.Now} - Semaphore was already released for {fullPath}{Environment.NewLine}{ex.Message}{Environment.NewLine}{ex.InnerException}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"{DateTime.Now} - Error in semaphore for {fullPath}{Environment.NewLine}{ex.Message}{Environment.NewLine}{ex.InnerException}");
            }

            _logger.LogDebug($"{DateTime.Now} - Finished handling event {changeType} for {fullPath}");

        }
        public async Task TerminateAsync()
        {
            foreach (var watcher in _fileSystemWatchers.Values)
            {
                UnwatchPath(watcher.Path);
            }

            foreach (var fileLocks in _fileEventsInProgress)
            {
                try
                {
                    _ = (fileLocks.Value.Semaphore?.Release());
                }
                catch { }
            }
            _fileEventsInProgress.Clear();
            _fileSystemWatchers.Clear();

            await Task.CompletedTask;
        }
    }
}
