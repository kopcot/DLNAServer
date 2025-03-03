using DLNAServer.Common;
using DLNAServer.Configuration;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.MediaContent.Interfaces;
using DLNAServer.Features.MediaProcessors.Interfaces;
using DLNAServer.Helpers.Logger;
using DLNAServer.Types.DLNA;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace DLNAServer.Features.MediaContent
{
    public partial class ContentExplorerManager : IContentExplorerManager, IDisposable
    {
        private readonly ILogger<ContentExplorerManager> _logger;
        private readonly ServerConfig _serverConfig;
        private readonly Lazy<IFileRepository> _fileRepositoryLazy;
        private readonly Lazy<IDirectoryRepository> _directoryRepositoryLazy;
        private readonly Lazy<IMemoryCache> _memoryCacheLazy;
        private readonly Lazy<IThumbnailDataRepository> _thumbnailDataRepositoryLazy;
        private readonly Lazy<IMediaProcessingService> _mediaProcessingServiceLazy;
        private IFileRepository FileRepository => _fileRepositoryLazy.Value;
        private IDirectoryRepository DirectoryRepository => _directoryRepositoryLazy.Value;
        private IMemoryCache MemoryCache => _memoryCacheLazy.Value;
        private IThumbnailDataRepository ThumbnailDataRepository => _thumbnailDataRepositoryLazy.Value;
        private IMediaProcessingService MediaProcessingService => _mediaProcessingServiceLazy.Value;
        public ContentExplorerManager(
            ILogger<ContentExplorerManager> logger,
            ServerConfig serverConfig,
            Lazy<IFileRepository> contentRepositoryLazy,
            Lazy<IDirectoryRepository> directoryRepositoryLazy,
            Lazy<IMemoryCache> memoryCacheLazy,
            Lazy<IThumbnailDataRepository> thumbnailDataRepositoryLazy,
            Lazy<IMediaProcessingService> mediaProcessingServiceLazy)
        {
            _logger = logger;
            _serverConfig = serverConfig;
            _fileRepositoryLazy = contentRepositoryLazy;
            _directoryRepositoryLazy = directoryRepositoryLazy;
            _memoryCacheLazy = memoryCacheLazy;
            _thumbnailDataRepositoryLazy = thumbnailDataRepositoryLazy;
            _mediaProcessingServiceLazy = mediaProcessingServiceLazy;
        }

        public async Task InitializeAsync()
        {
            var inputFiles = GetAllFilesInFolders(_serverConfig.SourceFolders, true);
            await RefreshFoundFilesAsync(inputFiles, true);

            var filesInDb = await FileRepository.GetAllAsync(useCachedResult: false);
            foreach (var filesInDbChunk in filesInDb.Chunk(200).ToList())
            {
                _ = await CheckFilesExistingAsync(filesInDbChunk);
            }

            var directoriesInDb = await DirectoryRepository.GetAllAsync(useCachedResult: false);
            foreach (var directoriesInDbChunk in directoriesInDb.Chunk(200).ToList())
            {
                _ = await CheckDirectoriesExistingAsync(directoriesInDbChunk);
            }

            InformationRefreshedInfo(directoriesInDb.Length, filesInDb.Length);
        }
        public Task TerminateAsync()
        {
            return Task.CompletedTask;
        }
        private Dictionary<DlnaMime, IEnumerable<string>> GetAllFilesInFolders(IEnumerable<string> sourceFolders, bool withSubdirectories)
        {
            List<string> filesInSourceFolders = [];

            foreach (var sourceFolder in sourceFolders.ToList())
            {
                var directory = new DirectoryInfo(sourceFolder);
                if (!directory.Exists)
                {
                    WarningDirectoryNotExists(sourceFolder);
                    continue;
                }
                // unable to use search patters from ServerConfig.Extensions,
                // as for Linux it is different between .jpg, .JPG, .Jpg
                // 'MatchCasing = MatchCasing.CaseInsensitive' is not helpful  
                var files = directory
                    .EnumerateFiles("*.*", new EnumerationOptions
                    {
                        RecurseSubdirectories = withSubdirectories,
                        AttributesToSkip = FileAttributes.None,
                        BufferSize = 1_024_000,
                        IgnoreInaccessible = false,
                        MatchCasing = MatchCasing.CaseInsensitive,
                        MatchType = MatchType.Simple,
                        MaxRecursionDepth = int.MaxValue,
                        ReturnSpecialDirectories = true,
                    })
                    .Select(static (f) => f.FullName);

                filesInSourceFolders
                    .AddRange(files);
            }

            Dictionary<DlnaMime, IEnumerable<string>> foundFiles = filesInSourceFolders
                .Where(f => !_serverConfig.ExcludeFolders.Any(skip => f.Contains(skip, StringComparison.InvariantCultureIgnoreCase)))
                .DistinctBy(static (f) => f)
                .OrderBy(static (f) => f)
                .GroupBy(f =>
                {
                    var extension = _serverConfig.MediaFileExtensions.FirstOrDefault(e => f.EndsWith(e.Key, StringComparison.InvariantCultureIgnoreCase));
                    return extension.Value.Key;
                })
                .Where(static (g) => g.Key != DlnaMime.Undefined)
                .ToDictionary(static (g) => g.Key, static (g) => g.ToArray().AsEnumerable());

            return foundFiles;
        }
        private static readonly SemaphoreSlim semaphoreRefreshFoundFiles = new(1, 1);
        /// <param name="inputFiles">Files to check and add to database</param>
        /// <param name="shouldBeAdded"><see langword="true"/> if <paramref name="inputFiles"/> should not exists in the database</param>
        /// <returns></returns>
        public async Task RefreshFoundFilesAsync(Dictionary<DlnaMime, IEnumerable<string>> inputFiles, bool shouldBeAdded)
        {
            try
            {
                _ = await semaphoreRefreshFoundFiles.WaitAsync(TimeSpanValues.Time5min);

                List<FileEntity> fileEntities = [];

                var existingFiles = await FileRepository.GetAllFileFullNamesAsync(useCachedResult: !shouldBeAdded);
                var existingFilesHash = new HashSet<string>(existingFiles);

                foreach (var mimeGroup in inputFiles.ToList())
                {
                    var fileExtensionConfiguration = _serverConfig.MediaFileExtensions.Values.FirstOrDefault(e => e.Key == mimeGroup.Key);

                    foreach (var file in mimeGroup.Value.ToList())
                    {
                        //if (existingFiles.Contains(file))
                        if (!existingFilesHash.Add(file)) // Add returns false if already exists in HashSet
                        {
                            continue;
                        }

                        FileInfo fileInfo = new(file);
                        if (fileInfo.Exists)
                        {
                            fileEntities.Add(new FileEntity()
                            {
                                FileCreateDate = fileInfo.CreationTime,
                                FileModifiedDate = fileInfo.LastWriteTime,
                                FileName = fileInfo.Name,
                                FileExtension = string.Intern(fileInfo.Extension.ToUpper(culture: System.Globalization.CultureInfo.InvariantCulture)),
                                Folder = fileInfo.Directory != null ? string.Intern(fileInfo.Directory!.FullName) : null,
                                FilePhysicalFullPath = fileInfo.FullName,
                                Title = fileInfo.Name,
                                FileSizeInBytes = fileInfo.Length,
                                FileDlnaMime = fileExtensionConfiguration.Key,
                                FileDlnaProfileName = fileExtensionConfiguration.Value != null
                                    ? string.Intern(fileExtensionConfiguration.Value) : mimeGroup.Key.ToMainProfileNameString(),
                                UpnpClass = mimeGroup.Key.ToDefaultDlnaItemClass(),
                            });
                        }
                    }
                }

                if (fileEntities.Count == 0)
                {
                    return;
                }

                var folders = fileEntities
                    .Select(static (f) => f.Folder)
                    .DistinctBy(static (f) => f)
                    .Where(static (f) => !string.IsNullOrWhiteSpace(f))
                    .ToArray();

                IEnumerable<DirectoryEntity> directoryEntities = await GetNewDirectoryEntities(folders);
                // Fill parent directory after creation of all directories
                await FillParentDirectoriesAsync(fileEntities, directoryEntities);

                if (directoryEntities.Any() || fileEntities.Count != 0)
                {
                    InformationTotalAdding(directoryEntities.Count(), fileEntities.Count);

                    const int maxShownCount = 10;
                    if (directoryEntities.Any())
                    {
                        InformationDirectoriesCount(
                            string.Join(Environment.NewLine, directoryEntities.Select(static (fe) => fe.DirectoryFullPath).Take(maxShownCount)),
                            directoryEntities.Count() > maxShownCount ? $"{Environment.NewLine}..." : string.Empty);

                        _ = await DirectoryRepository.AddRangeAsync(directoryEntities);
                    }
                    if (fileEntities.Count != 0)
                    {
                        InformationFilesCount(
                            string.Join(Environment.NewLine, fileEntities.Select(static (fe) => fe.FilePhysicalFullPath).Take(maxShownCount)),
                            directoryEntities.Count() > maxShownCount ? $"{Environment.NewLine}..." : string.Empty);

                        _ = await FileRepository.AddRangeAsync(fileEntities);
                    }
                    _ = await DirectoryRepository.GetAllAsync(asNoTracking: true, useCachedResult: false); // to refresh cached value
                    _ = await FileRepository.GetAllFileFullNamesAsync(useCachedResult: false); // to refresh cached value
                }
            }
            catch
            {
                throw;
            }
            finally
            {
                _ = semaphoreRefreshFoundFiles.Release();
            }
        }

        private async Task FillParentDirectoriesAsync(IEnumerable<FileEntity> fileEntities, IEnumerable<DirectoryEntity> directoryEntities)
        {
            var existingDirectoryEntities = await DirectoryRepository.GetAllAsync(useCachedResult: true);

            foreach (var directoryEntity in directoryEntities.ToList())
            {
                if (new DirectoryInfo(directoryEntity.DirectoryFullPath).Parent is DirectoryInfo parentDirectory)
                {
                    directoryEntity.ParentDirectory = directoryEntities.FirstOrDefault(de => de.DirectoryFullPath == parentDirectory.FullName)
                        ?? existingDirectoryEntities.FirstOrDefault(de => de.DirectoryFullPath == parentDirectory.FullName)
                        ?? throw new ApplicationException($"Parent directory not found for directory '{parentDirectory.FullName}'");
                }
                else
                {
                    DebugDirectoryWithoutParent(directoryEntity.DirectoryFullPath);
                }
            }
            foreach (var file in fileEntities.ToList())
            {
                file.Directory = directoryEntities.FirstOrDefault(d => d.DirectoryFullPath == file.Folder)
                    ?? existingDirectoryEntities.FirstOrDefault(de => de.DirectoryFullPath == file.Folder)
                    ?? throw new ApplicationException($"Parent directory not found for file '{file.Folder}'");
            }
        }
        public async Task<List<DirectoryEntity>> GetNewDirectoryEntities(IEnumerable<string?> folders)
        {
            var existingDirectories = await DirectoryRepository.GetAllDirectoryFullNamesAsync(useCachedResult: false);

            List<DirectoryEntity> directoryEntities = [];
            foreach (var folder in folders.ToList())
            {
                DirectoryInfo? directoryInfo = new(folder!);
                while (directoryInfo != null &&
                    directoryInfo.Exists)
                {
                    if (!existingDirectories.Contains(directoryInfo.FullName)
                        && !directoryEntities.Any(d => d.DirectoryFullPath == directoryInfo.FullName))
                    {
                        DirectoryEntity directoryEntity = new()
                        {
                            Directory = directoryInfo.Name,
                            DirectoryFullPath = directoryInfo.FullName,
                            ParentDirectory = null,
                            Depth = GetDirectoryDepth(directoryInfo.FullName),
                        };
                        directoryEntities.Add(directoryEntity);
                    }
                    else
                    {
                        break;
                    }

                    directoryInfo = directoryInfo.Parent;
                }
            }

            return directoryEntities;
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

        private async Task<(IEnumerable<FileEntity> files, IEnumerable<DirectoryEntity> directories)> GetFilesAndDirectoriesAsync(string objectID)
        {
            var directory = await DirectoryRepository.GetByIdAsync(objectID, asNoTracking: true, useCachedResult: true);
            IEnumerable<DirectoryEntity> directoryContainers;
            IEnumerable<FileEntity> filesItems;

            if (directory is DirectoryEntity)
            {
                // not possible to take cached result because some files can be removed during server offline time
                // and in next parts, there is check for existing file
                filesItems = await FileRepository.GetAllByParentDirectoryIdsAsync([directory.Id], _serverConfig.ExcludeFolders, useCachedResult: false);
                directoryContainers = await DirectoryRepository.GetAllParentsByDirectoriesIdAsync([directory.Id], _serverConfig.ExcludeFolders, useCachedResult: false);
            }
            else
            {
                filesItems = [];
                directoryContainers = await DirectoryRepository.GetAllByPathFullNamesAsync(_serverConfig.SourceFolders, useCachedResult: true);
            }

            return (files: filesItems, directories: directoryContainers);
        }
        private Task<(FileEntity[] files, DirectoryEntity[] directories)> GetFilesByLastAddedToDbAsync(uint numberOfFiles)
        {
            return FileRepository
                .GetAllByAddedToDbAsync((int)numberOfFiles, _serverConfig.ExcludeFolders, useCachedResult: false)
                .ContinueWith(fe => (fe.Result, Array.Empty<DirectoryEntity>()));
        }
        public async Task<IEnumerable<FileEntity>> CheckFilesExistingAsync(IEnumerable<FileEntity> fileEntities)
        {
            try
            {
                if (!fileEntities.Any())
                {
                    return fileEntities;
                }

                var notExistingFiles = new ConcurrentBag<FileEntity>();
                var existingFiles = new ConcurrentBag<(int order, FileEntity entity)>();

                var maxDegreeOfParallelism = Math.Min(fileEntities.Count(), (int)_serverConfig.ServerMaxDegreeOfParallelism);

                _ = Parallel.For(
                    0,
                    fileEntities.Count(),
                    parallelOptions: new() { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                    (index) =>
                    {
                        var file = fileEntities.ElementAt(index);
                        FileInfo fileInfo = new(file.FilePhysicalFullPath);
                        if (fileInfo.Exists)
                        {
                            existingFiles.Add((index, file));
                        }
                        else
                        {
                            InformationFileMissing(file.FilePhysicalFullPath);
                            notExistingFiles.Add(file);
                        }
                    });

                if (!notExistingFiles.IsEmpty)
                {
                    await ClearMetadataAsync(notExistingFiles);
                    await ClearThumbnailsAsync(notExistingFiles, true);

                    _ = await FileRepository.DeleteRangeAsync(notExistingFiles);
                }

                return existingFiles
                    .OrderBy(static (f) => f.order)
                    .Select(static (f) => f.entity)
                    .ToArray();
            }
            catch (Exception ex)
            {
                if (MemoryCache is MemoryCache memoryCache)
                {
                    var keys = memoryCache
                        .Keys
                        .OrderBy(static (k) => k)
                        .Select(static (k, i) => new KeyValuePair<string, object>($"Key_{i}", k))
                        .ToList();
                    InformationCachedKeys(string.Join(Environment.NewLine, keys));
                }
                _logger.LogGeneralErrorMessage(ex);
                return fileEntities;
            }
        }

        public async Task<IEnumerable<DirectoryEntity>> CheckDirectoriesExistingAsync(IEnumerable<DirectoryEntity> directoryEntities)
        {
            try
            {
                if (!directoryEntities.Any())
                {
                    return directoryEntities;
                }

                var notExistingDirectories = new ConcurrentBag<DirectoryEntity>();
                var existingDirectories = new ConcurrentBag<(int order, DirectoryEntity entity)>();

                var maxDegreeOfParallelism = Math.Min(directoryEntities.Count(), (int)_serverConfig.ServerMaxDegreeOfParallelism);

                _ = Parallel.For(
                    0,
                    directoryEntities.Count(),
                    parallelOptions: new() { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                    (index) =>
                    {
                        var directory = directoryEntities.ElementAt(index);
                        DirectoryInfo directoryInfo = new(directory.DirectoryFullPath);
                        if (directoryInfo.Exists)
                        {
                            existingDirectories.Add((index, directory));
                        }
                        else
                        {
                            InformationDirectoryMissing(directory.DirectoryFullPath);
                            notExistingDirectories.Add(directory);
                        }
                    });

                if (!notExistingDirectories.IsEmpty)
                {
                    var notExistingSubdirectories = await DirectoryRepository
                        .GetAllStartingByPathFullNamesAsync(
                            pathFullNames: notExistingDirectories.Select(static (ned) => ned.DirectoryFullPath),
                            useCachedResult: false);
                    if (notExistingSubdirectories.Length != 0)
                    {
                        foreach (var item in notExistingSubdirectories.ToList())
                        {
                            notExistingDirectories.Add(item);
                        }
                    }

                    var removeFiles = await FileRepository
                        .GetAllByParentDirectoryIdsAsync(notExistingDirectories.Select(static (ned) => ned.Id), [], useCachedResult: false);
                    if (removeFiles.Length != 0)
                    {
                        _ = await CheckFilesExistingAsync(removeFiles);
                    }

                    _ = await DirectoryRepository.DeleteRangeAsync(notExistingDirectories);
                }

                return existingDirectories
                    .OrderBy(static (f) => f.order)
                    .Select(static (f) => f.entity)
                    .ToArray();
            }
            catch (Exception ex)
            {
                if (MemoryCache is MemoryCache memoryCache)
                {
                    var keys = memoryCache
                        .Keys
                        .OrderBy(static (k) => k)
                        .Select(static (k, i) => new KeyValuePair<string, object>($"Key_{i}", k))
                        .ToList();
                    InformationCachedKeys(string.Join(Environment.NewLine, keys));
                }
                _logger.LogGeneralErrorMessage(ex);
                return directoryEntities;
            }
        }

        public async Task<(FileEntity[] fileEntities, DirectoryEntity[] directoryEntities, bool isRootFolder, uint totalMatches)> GetBrowseResultItems(
            string objectID,
            int startingIndex,
            int requestedCount
            )
        {
            var startTime = DateTime.Now;

            var directoryStartObject = await DirectoryRepository.GetByIdAsync(objectID, asNoTracking: true, useCachedResult: true);
            var getDirectoryTime = DateTime.Now;

            bool isRootFolder = directoryStartObject == null;
            var getAllFilesInDirectoryTime = DateTime.Now;
            var refreshFoundFilesTime = DateTime.Now;
            if (!isRootFolder)
            {
                // refresh directory for added files
                var inputFiles = GetAllFilesInFolders([directoryStartObject!.DirectoryFullPath], true);
                getAllFilesInDirectoryTime = DateTime.Now;
                await RefreshFoundFilesAsync(inputFiles, false);
                refreshFoundFilesTime = DateTime.Now;
            }

            (var fileEntities, var directoryEntities) = await GetFilesAndDirectoriesAsync(objectID);
            var getEntitiesTime = DateTime.Now;

            // sorting before checking root folder, as for sorting additional files in root folder is by timestamps
            fileEntities = fileEntities.OrderBy(static (f) => f.LC_Title).ToArray();
            directoryEntities = directoryEntities.OrderBy(static (d) => d.LC_Directory).ToArray();

            if (isRootFolder)
            {
                (var fileEntitiesAdded, var directoryEntitiesAdded) = await GetFilesByLastAddedToDbAsync(_serverConfig.CountOfFilesByLastAddedToDb);

                fileEntities = fileEntities.Concat(fileEntitiesAdded).ToArray();
                directoryEntities = directoryEntities.Concat(directoryEntitiesAdded).ToArray();
            }
            var addAdditionalEntitiesTime = DateTime.Now;

            uint totalMatches = _serverConfig.ServerIgnoreRequestedCountAttributeFromRequest
                ? (uint)(fileEntities.Count() + directoryEntities.Count())
                : FilterEntities(startingIndex, requestedCount, ref fileEntities, ref directoryEntities);
            var filterEntitiesTime = DateTime.Now;

            // possible to return less objects with this checking, but client will request rest of them in next request
            var countBeforeCheck = fileEntities.Count() + directoryEntities.Count();

            fileEntities = await CheckFilesExistingAsync(fileEntities);
            directoryEntities = await CheckDirectoriesExistingAsync(directoryEntities);
            var checkEntitiesTime = DateTime.Now;

            await MediaProcessingService.FillEmptyMetadataAsync(fileEntities, setCheckedForFailed: true);
            await MediaProcessingService.FillEmptyThumbnailsAsync(fileEntities, setCheckedForFailed: true);
            var endTime = DateTime.Now;

            if (countBeforeCheck != (fileEntities.Count() + directoryEntities.Count()))
            {
                WarningObjectsRemovedFromDirectory(
                    objectID,
                    directoryStartObject?.DirectoryFullPath,
                    countBeforeCheck,
                    fileEntities.Count() + directoryEntities.Count());
            }

            if (_serverConfig.ServerShowDurationDetailsBrowseRequest)
            {
                InformationBrowseDetailInfo(
                    objectID: objectID,
                    startTime: startTime,
                    endTime: endTime,
                    getDirectory: (getDirectoryTime - startTime).TotalMilliseconds,
                    getFiles: (getAllFilesInDirectoryTime - getDirectoryTime).TotalMilliseconds,
                    refreshFoundFiles: (refreshFoundFilesTime - getAllFilesInDirectoryTime).TotalMilliseconds,
                    getDataFromDatabase: (getEntitiesTime - refreshFoundFilesTime).TotalMilliseconds,
                    addAdditionalDataFromDatabase: (addAdditionalEntitiesTime - getEntitiesTime).TotalMilliseconds,
                    filterData: (filterEntitiesTime - addAdditionalEntitiesTime).TotalMilliseconds,
                    checkFiles: (checkEntitiesTime - filterEntitiesTime).TotalMilliseconds,
                    fillEmptyData: (endTime - checkEntitiesTime).TotalMilliseconds,
                    totalDuration: (endTime - startTime).TotalMilliseconds,
                    directory: directoryStartObject?.DirectoryFullPath
                    );
            }

            return (fileEntities.ToArray(), directoryEntities.ToArray(), isRootFolder, totalMatches);
        }

        private static uint FilterEntities(int startingIndex, int requestedCount, ref IEnumerable<FileEntity> fileEntities, ref IEnumerable<DirectoryEntity> directoryEntities)
        {
            var directoryCount = directoryEntities.Count();
            var fileCount = fileEntities.Count();

            uint totalMatches = (uint)(directoryCount + fileCount);

            directoryEntities = directoryEntities.Skip(startingIndex).Take(requestedCount).ToArray();
            if (directoryCount == 0)
            {
                fileEntities = fileEntities.Skip(startingIndex).Take(requestedCount).ToArray();
            }
            else if (directoryEntities.Count() < (requestedCount))
            {
                fileEntities = fileEntities.Skip(startingIndex - directoryCount).Take(requestedCount - directoryEntities.Count()).ToArray();
            }
            else
            {
                fileEntities = [];
            }

            return totalMatches;
        }
        public async Task ClearAllThumbnailsAsync(bool deleteThumbnailFile = true)
        {
            var files = await FileRepository.GetAllAsync(useCachedResult: false);
            await ClearThumbnailsAsync(files, deleteThumbnailFile);
        }
        public async Task ClearThumbnailsAsync(IEnumerable<FileEntity> files, bool deleteThumbnailFile = true)
        {
            files = files.Where(static (f) => f != null);

            FileInfo fileInfo;

            foreach (var file in files.ToList())
            {
                if (deleteThumbnailFile
                    && !string.IsNullOrEmpty(file.Thumbnail?.ThumbnailFilePhysicalFullPath))
                {
                    fileInfo = new(file.Thumbnail.ThumbnailFilePhysicalFullPath);
                    if (fileInfo.Exists)
                    {
                        fileInfo.Delete();
                    }
                }

                file.IsThumbnailChecked = false;

                if (file.Thumbnail != null)
                {
                    if (file.Thumbnail.ThumbnailDataId.HasValue)
                    {
                        var thumbnailData = await ThumbnailDataRepository.GetByIdAsync(file.Thumbnail.ThumbnailDataId.Value, useCachedResult: false);
                        if (thumbnailData != null)
                        {
                            ThumbnailDataRepository.MarkForDelete(thumbnailData);
                        }
                        file.Thumbnail.ThumbnailData = null;
                    }
                    FileRepository.MarkForDelete(file.Thumbnail);
                    file.Thumbnail = null;
                }
            }
            _ = await FileRepository.SaveChangesAsync();

            _ = await FileRepository.DbContext.Database.ExecuteSqlRawAsync("VACUUM;");
        }
        public async Task ClearAllMetadataAsync()
        {
            var files = await FileRepository.GetAllAsync(useCachedResult: false);
            await ClearMetadataAsync(files);
        }
        public async Task ClearMetadataAsync(IEnumerable<FileEntity> files)
        {
            foreach (var file in files.ToList())
            {
                if (file.AudioMetadata != null)
                {
                    FileRepository.MarkForDelete(file.AudioMetadata);
                    file.AudioMetadata = null;
                }
                if (file.VideoMetadata != null)
                {
                    FileRepository.MarkForDelete(file.VideoMetadata);
                    file.VideoMetadata = null;
                }
                if (file.SubtitleMetadata != null)
                {
                    FileRepository.MarkForDelete(file.SubtitleMetadata);
                    file.SubtitleMetadata = null;
                }
                file.IsMetadataChecked = false;
            }
            _ = await FileRepository.SaveChangesAsync();

            _ = await FileRepository.DbContext.Database.ExecuteSqlRawAsync("VACUUM;");
        }

        #region Dispose
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ContentExplorer()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion Dispose
    }
}
