﻿using DLNAServer.Configuration;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.Cache.Interfaces;
using DLNAServer.Features.FileWatcher.Interfaces;
using DLNAServer.Features.MediaContent.Interfaces;
using DLNAServer.Features.MediaProcessors.Interfaces;
using DLNAServer.Helpers.Logger;
using DLNAServer.Types.DLNA;

namespace DLNAServer.Features.FileWatcher
{
    public partial class FileWatcherManager : IFileWatcherManager
    {
        private readonly Lazy<IFileWatcherHandler> _fileWatcherServiceLazy;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private ILogger<FileWatcherManager> _logger;
        private readonly ServerConfig _serverConfig;
        private IFileWatcherHandler FileWatcherService => _fileWatcherServiceLazy.Value;
        private static bool _areFileWatcherEventsAdded = false;
        public FileWatcherManager(
            ILogger<FileWatcherManager> logger,
            ServerConfig serverConfig,
            IServiceScopeFactory serviceScopeFactory,
            Lazy<IFileWatcherHandler> fileWatcherServiceLazy)
        {
            _logger = logger;
            _serverConfig = serverConfig;
            _serviceScopeFactory = serviceScopeFactory;
            _fileWatcherServiceLazy = fileWatcherServiceLazy;
        }
        private static ulong _updatesCount = 0;
        public ulong UpdatesCount
        {
            get
            {
                if (_updatesCount >= uint.MaxValue)
                {
                    _updatesCount = uint.MinValue;
                }

                return _updatesCount;
            }
        }

        public async Task InitializeAsync()
        {
            await InitWatchingFilesAtSourceFoldersAsync();
            InformationStartedWatchingSourceFolders();
        }
        public Task TerminateAsync()
        {
            _areFileWatcherEventsAdded = false;
            _updatesCount = 0;

            return Task.CompletedTask;
        }
        private Task InitWatchingFilesAtSourceFoldersAsync()
        {
            if (!_areFileWatcherEventsAdded)
            {
                foreach (var sourceFolder in _serverConfig.SourceFolders)
                {
                    FileWatcherService.WatchPath(sourceFolder);
                }

                _areFileWatcherEventsAdded = true;
            }

            return Task.CompletedTask;
        }
        public async Task HandleFileCreatedChanged(string fileFullPath, WatcherChangeTypes eventAction, DateTime eventTimestamp)
        {
            FileInfo fileInfo = new(fileFullPath);
            if (!fileInfo.Exists)
            {
                WarningFileNotExists(eventAction, fileFullPath);
                await HandleFileRemove(fileFullPath, WatcherChangeTypes.Deleted, eventTimestamp);
                return;
            }

            await HandleEventScope(async (scope) =>
            {
                var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
                var contentExplorerManager = scope.ServiceProvider.GetRequiredService<IContentExplorerManager>();

                var fileEntities = await fileRepository.GetAllByPathFullNameAsync(fileFullPath, false);
                if (fileEntities == null || !fileEntities.Any())
                {
                    DebugAddToDatabase(eventAction, fileFullPath);

                    var dlnaMime = GetConfiguredDlnaMimeFromFileExtension(fileInfo.Extension);
                    var inputFile = new Dictionary<DlnaMime, IEnumerable<string>> { { dlnaMime, [fileFullPath] } };

                    await contentExplorerManager.RefreshFoundFilesAsync(inputFile, true);
                }
                else
                {
                    DebugExistsInDatabase(eventAction, fileFullPath);
                }
                if (_serverConfig.GenerateMetadataAndThumbnailsAfterAdding)
                {
                    var newFileEntities = await fileRepository.GetAllByPathFullNameAsync(fileFullPath, false) ?? throw new NullReferenceException();
                    if (!newFileEntities.Any())
                    {
                        WarningFileRecordNotCreated(eventAction, fileFullPath);
                    }
                    else if (newFileEntities.Length != 1)
                    {
                        WarningFileRecordMultiple(eventAction, fileFullPath);
                    }
                    else
                    {
                        var mediaProcessingService = scope.ServiceProvider.GetRequiredService<IMediaProcessingService>();

                        await mediaProcessingService.FillEmptyMetadataAsync(newFileEntities, false);
                        await mediaProcessingService.FillEmptyThumbnailsAsync(newFileEntities, false);
                    }
                }
            }, eventAction, fileFullPath, eventTimestamp);
        }
        public async Task HandleFileRenamed(string newFileFullPath, string oldFileFullPath, WatcherChangeTypes eventAction, DateTime eventTimestamp)
        {
            FileInfo fileInfo = new(newFileFullPath);
            if (!fileInfo.Exists)
            {
                WarningFileNotExists(eventAction, newFileFullPath);
                await HandleFileRemove(newFileFullPath, WatcherChangeTypes.Deleted, eventTimestamp);
                return;
            }

            await HandleEventScope(async (scope) =>
            {
                var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();

                var existingNewFiles = await fileRepository.GetAllByPathFullNameAsync(newFileFullPath, useCachedResult: false);
                if (existingNewFiles.Any())
                {
                    await HandleFileRemove(newFileFullPath, WatcherChangeTypes.Deleted, eventTimestamp);
                }

                var existingOldFiles = await fileRepository.GetAllByPathFullNameAsync(oldFileFullPath, useCachedResult: false);
                if (existingOldFiles.Length == 0)
                {
                    var dlnaMime = GetConfiguredDlnaMimeFromFileExtension(fileInfo.Extension);
                    if (dlnaMime == DlnaMime.Undefined)
                    {
                        WarningFileExtensionUndefined(eventAction, fileInfo.Extension, fileInfo.FullName);
                    }
                    Dictionary<DlnaMime, IEnumerable<string>> inputFile = new() { { dlnaMime, [newFileFullPath] } };

                    var contentExplorerManager = scope.ServiceProvider.GetRequiredService<IContentExplorerManager>();
                    await contentExplorerManager.RefreshFoundFilesAsync(inputFile, true);
                }
                else
                {
                    await UpdateRenamedFile(scope, existingOldFiles, fileInfo);

                    var fileMemoryCache = scope.ServiceProvider.GetRequiredService<IFileMemoryCacheManager>();
                    fileMemoryCache.EvictSingleFile(oldFileFullPath);
                }
            }, eventAction, newFileFullPath, eventTimestamp);
        }
        public async Task HandleFileRemove(string fileFullPath, WatcherChangeTypes eventAction, DateTime eventTimestamp)
        {
            await HandleEventScope(async (scope) =>
            {
                var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
                var thumbnailRepository = scope.ServiceProvider.GetRequiredService<IThumbnailRepository>();

                DebugFileRemove(eventAction, fileFullPath);
                var files = await fileRepository.GetAllByPathFullNameAsync(fileFullPath, false);
                if (files == null || !files.Any())
                {
                    return;
                }

                await PrepareToRemoveEntity(fileRepository, thumbnailRepository, files);

                _ = await fileRepository.DeleteRangeAsync(files);

                var fileMemoryCache = scope.ServiceProvider.GetRequiredService<IFileMemoryCacheManager>();
                fileMemoryCache.EvictSingleFile(fileFullPath);

                DebugFileRemoveDone(eventAction, fileFullPath);

            }, eventAction, fileFullPath, eventTimestamp);
        }

        private static async Task PrepareToRemoveEntity(IFileRepository fileRepository, IThumbnailRepository thumbnailRepository, IEnumerable<FileEntity> files)
        {
            if (!files.Any())
            {
                return;
            }

            var thumbnailEntitiesIds = files
                .Where(static (f) => f.ThumbnailId.HasValue)
                .Select(static (f) => f.ThumbnailId!.Value)
                .ToArray();

            if (thumbnailEntitiesIds.Length > 0)
            {
                var thumbnailEntities = await thumbnailRepository.GetAllByIdsAsync(thumbnailEntitiesIds);

                if (thumbnailEntities.Any())
                {
                    DeleteThumbnailsIfExists(ref thumbnailEntities);
                    foreach (var thumbnailEntity in thumbnailEntities.ToList())
                    {
                        thumbnailRepository.MarkForDelete(thumbnailEntity.ThumbnailData);
                        thumbnailRepository.MarkForDelete(thumbnailEntity);
                    }
                }
            }

            files
                .Where(static (nef) => nef.AudioMetadata != null)
                .Select(static (nef) => nef.AudioMetadata!)
                .ToList()
                .ForEach(td => fileRepository.MarkForDelete(td));
            files
                .Where(static (nef) => nef.VideoMetadata != null)
                .Select(static (nef) => nef.VideoMetadata!)
                .ToList()
                .ForEach(td => fileRepository.MarkForDelete(td));
            files
                .Where(static (nef) => nef.SubtitleMetadata != null)
                .Select(static (nef) => nef.SubtitleMetadata!)
                .ToList()
                .ForEach(td => fileRepository.MarkForDelete(td));

            foreach (var notExistingFile in files.ToList())
            {
                if (notExistingFile.Thumbnail?.ThumbnailDataId.HasValue == true)
                {
                    notExistingFile.Thumbnail.ThumbnailData = null;
                }
                notExistingFile.Thumbnail = null;
                notExistingFile.AudioMetadata = null;
                notExistingFile.VideoMetadata = null;
                notExistingFile.SubtitleMetadata = null;
            }
        }
        public async Task HandleDirectoryRemove(string fileFullPath, WatcherChangeTypes eventAction, DateTime eventTimestamp)
        {
            await HandleEventScope(async (scope) =>
            {
                var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
                var directoryRepository = scope.ServiceProvider.GetRequiredService<IDirectoryRepository>();
                var thumbnailRepository = scope.ServiceProvider.GetRequiredService<IThumbnailRepository>();

                var directories = await directoryRepository.GetAllStartingByPathFullNameAsync(fileFullPath, false);
                var files = await fileRepository.GetAllByParentDirectoryIdsAsync(directories.Select(static (d) => d.Id), [], false);

                await PrepareToRemoveEntity(fileRepository, thumbnailRepository, files);
                _ = await fileRepository.DeleteRangeAsync(files);
                _ = await directoryRepository.DeleteRangeAsync(directories);
            }, eventAction, fileFullPath, eventTimestamp);
        }

        public async Task HandleDirectoryRenamed(string newDirectoryFullPath, string oldDirectoryFullPath, WatcherChangeTypes eventAction, DateTime eventTimestamp)
        {
            if (!Directory.Exists(newDirectoryFullPath))
            {
                WarningDirectoryNotExists(eventAction, newDirectoryFullPath);
                return;
            }

            await HandleEventScope(async (scope) =>
            {
                var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
                var directoryRepository = scope.ServiceProvider.GetRequiredService<IDirectoryRepository>();

                var directories = await directoryRepository.GetAllStartingByPathFullNameAsync(oldDirectoryFullPath, false);
                var files = await fileRepository.GetAllByParentDirectoryIdsAsync(directories.Select(static (d) => d.Id), [], false);
                UpdateFilePaths(ref files, ref oldDirectoryFullPath, ref newDirectoryFullPath);
                UpdateDirectoryPaths(ref directories, ref oldDirectoryFullPath, ref newDirectoryFullPath);

                // Save entities into database, as next function are getting dbSet.AsNoTracking() results
                _ = await directoryRepository.SaveChangesAsync();
                _ = await fileRepository.SaveChangesAsync();

                var contentExplorerManager = scope.ServiceProvider.GetRequiredService<IContentExplorerManager>();
                var newDirectories = await contentExplorerManager.GetNewDirectoryEntities(directories.Select(static (d) => d.DirectoryFullPath));
                directories = directories.Concat(newDirectories).Distinct().ToArray();

                var existingDirectoryEntities = await directoryRepository.GetAllAsync(useCachedResult: false);

                FillParentDirectoriesAsync(ref existingDirectoryEntities, directories);
                FillParentDirectoriesAsync(ref existingDirectoryEntities, ref files, directories);

                _ = await directoryRepository.SaveChangesAsync();
                _ = await fileRepository.SaveChangesAsync();
            }, eventAction, newDirectoryFullPath, eventTimestamp);
        }
        private static async Task UpdateRenamedFile(IServiceScope scope, IEnumerable<FileEntity> files, FileInfo fileInfo)
        {
            var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
            var directoryRepository = scope.ServiceProvider.GetRequiredService<IDirectoryRepository>();
            var thumbnailRepository = scope.ServiceProvider.GetRequiredService<IThumbnailRepository>();

            var file = files.First();
            var filesToRemove = files.Where(f => f != file);

            file.FileName = fileInfo.Name;
            file.Title = fileInfo.Name;
            file.FilePhysicalFullPath = fileInfo.FullName;
            file.Folder = fileInfo.Directory?.FullName;
            file.FileModifiedDate = fileInfo.LastWriteTime;
            file.FileExtension = fileInfo.Extension;

            if (file.AudioMetadata != null)
            {
                file.AudioMetadata.FilePhysicalFullPath = file.FilePhysicalFullPath;
            }

            if (file.VideoMetadata != null)
            {
                file.VideoMetadata.FilePhysicalFullPath = file.FilePhysicalFullPath;
            }

            if (file.SubtitleMetadata != null)
            {
                file.SubtitleMetadata.FilePhysicalFullPath = file.FilePhysicalFullPath;
            }

            if (file.ThumbnailId.HasValue)
            {
                var thumbnailEntity = await thumbnailRepository.GetByIdAsync(file.ThumbnailId.Value, asNoTracking: true, useCachedResult: true);

                if (thumbnailEntity != null)
                {
                    ThumbnailEntity[] thumbnailEntities = [thumbnailEntity];
                    DeleteThumbnailsIfExists(ref thumbnailEntities);
                    fileRepository.MarkForDelete(file.Thumbnail);
                    thumbnailRepository.MarkForDelete(thumbnailEntity.ThumbnailData);
                    file.IsThumbnailChecked = false;
                    file.Thumbnail = null;
                }
            }

            var contentExplorerManager = scope.ServiceProvider.GetRequiredService<IContentExplorerManager>();
            var newDirectoryEntities = await contentExplorerManager.GetNewDirectoryEntities([fileInfo.Directory!.FullName]);
            var existingDirectoryEntities = await directoryRepository.GetAllAsync(useCachedResult: false);

            FileEntity[] fileEntities = [file];
            FillParentDirectoriesAsync(ref existingDirectoryEntities, newDirectoryEntities);
            FillParentDirectoriesAsync(ref existingDirectoryEntities, ref fileEntities, newDirectoryEntities);

            await PrepareToRemoveEntity(fileRepository, thumbnailRepository, filesToRemove);

            _ = await directoryRepository.AddRangeAsync(newDirectoryEntities);
            _ = await fileRepository.SaveChangesAsync();
            _ = await fileRepository.DeleteRangeAsync(filesToRemove);
        }
        private static void UpdateFilePaths(ref FileEntity[] files, ref string oldPath, ref string newPath)
        {
            foreach (var file in files.ToList())
            {
                bool isFileInSameDirectory = file.Folder!.Equals(oldPath, StringComparison.InvariantCultureIgnoreCase);
                if (isFileInSameDirectory)
                {
                    file.Folder = file.Folder!.Replace(oldPath, newPath);
                    file.FilePhysicalFullPath = file.FilePhysicalFullPath.Replace(oldPath, newPath);
                    if (file.Thumbnail != null)
                    {
                        file.Thumbnail.FilePhysicalFullPath = file.Thumbnail.FilePhysicalFullPath.Replace(oldPath, newPath);
                    }
                }
                else
                {
                    file.Folder = file.Folder!.Replace(oldPath + Path.DirectorySeparatorChar, newPath + Path.DirectorySeparatorChar);
                    file.FilePhysicalFullPath = file.FilePhysicalFullPath.Replace(oldPath + Path.DirectorySeparatorChar, newPath + Path.DirectorySeparatorChar);
                    if (file.Thumbnail != null)
                    {
                        file.Thumbnail.FilePhysicalFullPath = file.Thumbnail.FilePhysicalFullPath.Replace(oldPath + Path.DirectorySeparatorChar, newPath + Path.DirectorySeparatorChar);
                    }
                }
            }
        }
        private static void UpdateDirectoryPaths(ref DirectoryEntity[] directories, ref string oldPath, ref string newPath)
        {
            DirectoryInfo directoryInfo;
            foreach (var directory in directories.ToList())
            {
                directory.DirectoryFullPath = directory.DirectoryFullPath.Replace(oldPath, newPath);

                directoryInfo = new(directory.DirectoryFullPath);

                directory.Directory = directoryInfo.Name;
                directory.Depth = GetDirectoryDepth(directory.DirectoryFullPath);
            }
        }
        private DlnaMime GetConfiguredDlnaMimeFromFileExtension(string fileExtension) =>
            _serverConfig
                .MediaFileExtensions
                // Contains() as Linux is adding for example ".jpg[1]" to the end of file, if file is exists there
                .FirstOrDefault(ex => fileExtension.Contains(ex.Key, StringComparison.OrdinalIgnoreCase))
                .Value
                .Key;
        private static void DeleteThumbnailsIfExists(ref ThumbnailEntity[] thumbnails)
        {
            FileInfo thumbnailInfo;
            foreach (var thumbnail in thumbnails.ToList())
            {
                if (string.IsNullOrWhiteSpace(thumbnail.ThumbnailFilePhysicalFullPath))
                {
                    continue;
                }

                thumbnailInfo = new(thumbnail.ThumbnailFilePhysicalFullPath);
                var thumbnailDirectory = thumbnailInfo.Directory;
                if (thumbnailInfo.Exists)
                {
                    thumbnailInfo.Delete();
                }
                if (thumbnailDirectory?.Exists == true)
                {
                    int thumbnailDirectory_SubDirectories = thumbnailDirectory.EnumerateDirectories().Count();
                    int thumbnailDirectory_SubFiles = thumbnailDirectory.EnumerateFiles().Count();
                    if (thumbnailDirectory_SubDirectories == 0 &&
                        thumbnailDirectory_SubFiles == 0)
                    {
                        thumbnailDirectory.Delete(true);
                    }
                }
            }
        }
        private async Task HandleEventScope(Func<IServiceScope, Task> fileOperation, WatcherChangeTypes action, string filePath, DateTime eventTimestamp)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                _logger = scope.ServiceProvider.GetRequiredService<ILogger<FileWatcherManager>>();

                Guid guid = Guid.NewGuid();

                try
                {
                    DebugEventStarted(action, guid, eventTimestamp);
                    await fileOperation(scope);
                    DebugEventSuccessful(action, guid, eventTimestamp);
                }
                catch (Exception ex)
                {
                    WarningEventFailed(action, guid, eventTimestamp);
                    LoggerHelper.LogGeneralErrorMessage(_logger, ex);
                }

                _updatesCount++;
            }
        }
        private static void FillParentDirectoriesAsync(ref DirectoryEntity[] existingDirectoryEntities, ref FileEntity[] fileEntities, IEnumerable<DirectoryEntity> directoryEntities)
        {
            foreach (var file in fileEntities.ToList())
            {
                file.Directory = directoryEntities.FirstOrDefault(d => d.DirectoryFullPath == file.Folder)
                    ?? existingDirectoryEntities.FirstOrDefault(de => de.DirectoryFullPath == file.Folder);
            }
        }

        private static void FillParentDirectoriesAsync(ref DirectoryEntity[] existingDirectoryEntities, IEnumerable<DirectoryEntity> directoryEntities)
        {
            foreach (var directoryEntity in directoryEntities.ToList())
            {
                var parentDirectory = new DirectoryInfo(directoryEntity.DirectoryFullPath).Parent?.FullName;
                directoryEntity.ParentDirectory = directoryEntities.FirstOrDefault(de => de.DirectoryFullPath == parentDirectory)
                    ?? existingDirectoryEntities.FirstOrDefault(de => de.DirectoryFullPath == parentDirectory);
            }
        }
        private static int GetDirectoryDepth(string? actualFolder)
        {
            if (actualFolder == null)
            {
                return 0;
            }

            DirectoryInfo directoryInfoDepthCount = new(actualFolder);
            int depth = 0;
            while (directoryInfoDepthCount.Parent != null)
            {
                depth++;
                directoryInfoDepthCount = directoryInfoDepthCount.Parent;
            }
            return depth;
        }
    }
}
