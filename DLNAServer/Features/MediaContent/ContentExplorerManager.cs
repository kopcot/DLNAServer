using DLNAServer.Configuration;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.MediaContent.Interfaces;
using DLNAServer.Features.MediaProcessors.Interfaces;
using DLNAServer.Types.DLNA;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Buffers;
using System.Collections.Concurrent;
using System.Text;

namespace DLNAServer.Features.MediaContent
{
    public class ContentExplorerManager : IContentExplorerManager, IDisposable
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
            foreach (var filesInDbChunk in filesInDb.Chunk(200))
            {
                _ = await CheckFilesExistingAsync(filesInDbChunk);
            }

            var directoriesInDb = await DirectoryRepository.GetAllAsync(useCachedResult: false);
            foreach (var directoriesInDbChunk in directoriesInDb.Chunk(200))
            {
                _ = await CheckDirectoriesExistingAsync(directoriesInDbChunk);
            }

            _logger.LogInformation($"Refreshed {directoriesInDb.Count()} directories and {filesInDb.Count()} files.");
        }
        public async Task TerminateAsync()
        {
            await Task.CompletedTask;
        }
        private Dictionary<DlnaMime, IEnumerable<string>> GetAllFilesInFolders(IEnumerable<string> sourceFolders, bool withSubdirectories)
        {
            Dictionary<DlnaMime, IEnumerable<string>> foundFiles = [];
            List<string> filesInSourceFolders = [];

            foreach (var sourceFolder in sourceFolders)
            {
                var directory = new DirectoryInfo(sourceFolder);
                if (!directory.Exists)
                {
                    _logger.LogWarning($"{DateTime.Now} - Directory not exists: {sourceFolder}");
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
                    .Select(static (f) => f.FullName)
                    .ToArray();
                filesInSourceFolders
                    .AddRange(files);
            }

            foundFiles = filesInSourceFolders
                .Where(f => !_serverConfig.ExcludeFolders.Any(skip => f.Contains(skip)))
                .DistinctBy(static (f) => f)
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
                _ = await semaphoreRefreshFoundFiles.WaitAsync(TimeSpan.FromMinutes(5));

                List<FileEntity> fileEntities = [];

                var existingFiles = await FileRepository.GetAllFileFullNamesAsync(useCachedResult: !shouldBeAdded);
                var existingFilesHash = new HashSet<string>(existingFiles);

                foreach (var mimeGroup in inputFiles)
                {
                    var fileExtensionConfiguration = _serverConfig.MediaFileExtensions.Values.FirstOrDefault(e => e.Key == mimeGroup.Key);

                    foreach (var file in mimeGroup.Value)
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
                                FileExtension = fileInfo.Extension.ToUpper(),
                                Folder = fileInfo.Directory?.FullName,
                                FilePhysicalFullPath = fileInfo.FullName,
                                Title = fileInfo.Name,
                                FileSizeInBytes = fileInfo.Length,
                                FileDlnaMime = fileExtensionConfiguration.Key,
                                FileDlnaProfileName = fileExtensionConfiguration.Value ?? mimeGroup.Key.ToMainProfileNameString(),
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
                    _logger.LogInformation($"Total adding {directoryEntities.Count()} directory(ies) and {fileEntities.Count} file(s)");

                    const int maxShownCount = 10;
                    if (directoryEntities.Any())
                    {
                        StringBuilder sb = new();
                        _ = sb.AppendLine("Directories: ");
                        _ = sb.AppendLine(string.Join(Environment.NewLine, directoryEntities.Select(fe => fe.DirectoryFullPath).Take(maxShownCount)));
                        if (directoryEntities.Count() > maxShownCount)
                        {
                            _ = sb.AppendLine("...");
                        }

                        _logger.LogInformation(sb.ToString());

                        _ = await DirectoryRepository.AddRangeAsync(directoryEntities);
                        _ = await DirectoryRepository.GetAllAsync(useCachedResult: false); // to refresh cached value
                    }
                    if (fileEntities.Count != 0)
                    {
                        StringBuilder sb = new();
                        _ = sb.AppendLine("Files: ");
                        _ = sb.AppendLine(string.Join(Environment.NewLine, fileEntities.Select(fe => fe.FilePhysicalFullPath).Take(maxShownCount)));
                        if (fileEntities.Count > maxShownCount)
                        {
                            _ = sb.AppendLine("...");
                        }

                        _logger.LogInformation(sb.ToString());

                        _ = await FileRepository.AddRangeAsync(fileEntities);
                        _ = await FileRepository.GetAllFileFullNamesAsync(useCachedResult: false); // to refresh cached value
                    }
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

            foreach (var directoryEntity in directoryEntities)
            {
                if (new DirectoryInfo(directoryEntity.DirectoryFullPath).Parent is DirectoryInfo parentDirectory)
                {
                    directoryEntity.ParentDirectory = directoryEntities.FirstOrDefault(de => de.DirectoryFullPath == parentDirectory.FullName)
                        ?? existingDirectoryEntities.FirstOrDefault(de => de.DirectoryFullPath == parentDirectory.FullName)
                        ?? throw new ApplicationException($"Parent directory not found for directory '{parentDirectory.FullName}'");
                }
                else
                {
                    _logger.LogWarning($"Directory '{directoryEntity.DirectoryFullPath}' is without parent");
                }
            }
            foreach (var file in fileEntities)
            {
                file.Directory = directoryEntities.FirstOrDefault(d => d.DirectoryFullPath == file.Folder)
                    ?? existingDirectoryEntities.FirstOrDefault(de => de.DirectoryFullPath == file.Folder)
                    ?? throw new ApplicationException($"Parent directory not found for file '{file.Folder}'");
            }
        }
        public async Task<IEnumerable<DirectoryEntity>> GetNewDirectoryEntities(IEnumerable<string?> folders)
        {
            var existingDirectories = await DirectoryRepository.GetAllDirectoryFullNamesAsync(useCachedResult: false);

            List<DirectoryEntity> directoryEntities = [];
            foreach (var folder in folders)
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
        private async Task<(IEnumerable<FileEntity> files, IEnumerable<DirectoryEntity> directories)> GetFilesByLastAddedToDbAsync(uint numberOfFiles)
        {
            var files = await FileRepository.GetAllByAddedToDbAsync((int)numberOfFiles, _serverConfig.ExcludeFolders, useCachedResult: false);
            var directories = Enumerable.Empty<DirectoryEntity>();
            return (files, directories);
        }
        private async Task<IEnumerable<FileEntity>> CheckFilesExistingAsync(IEnumerable<FileEntity> fileEntities)
        {
            try
            {
                if (!fileEntities.Any())
                {
                    return fileEntities;
                }

                var notExistingFiles = new ConcurrentBag<FileEntity>();
                var existingFiles = new ConcurrentBag<(int order, FileEntity entity)>();

                _ = Parallel.For(
                    0,
                    fileEntities.Count(),
                    parallelOptions: new() { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
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
                            _logger.LogInformation($"{DateTime.Now} - File missing {file.FilePhysicalFullPath}");
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
                    _logger.LogInformation($"Cached keys:{Environment.NewLine}{string.Join(Environment.NewLine, keys)}");
                }
                _logger.LogError(ex, ex.Message);
                return fileEntities;
            }
        }

        private async Task<IEnumerable<DirectoryEntity>> CheckDirectoriesExistingAsync(IEnumerable<DirectoryEntity> directoryEntities)
        {
            try
            {
                if (!directoryEntities.Any())
                {
                    return directoryEntities;
                }

                var notExistingDirectories = new ConcurrentBag<DirectoryEntity>();
                var existingDirectories = new ConcurrentBag<(int order, DirectoryEntity entity)>();

                _ = Parallel.For(
                    0,
                    directoryEntities.Count(),
                    parallelOptions: new() { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
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
                            _logger.LogInformation($"{DateTime.Now} - Directory missing {directory.DirectoryFullPath}");
                            notExistingDirectories.Add(directory);
                        }
                    });

                if (!notExistingDirectories.IsEmpty)
                {
                    var notExistingSubdirectories = await DirectoryRepository
                        .GetAllStartingByPathFullNamesAsync(
                            pathFullNames: notExistingDirectories.Select(static (ned) => ned.DirectoryFullPath),
                            useCachedResult: false);
                    if (notExistingSubdirectories.Any())
                    {
                        foreach (var item in notExistingSubdirectories)
                        {
                            notExistingDirectories.Add(item);
                        }
                    }

                    var removeFiles = await FileRepository
                        .GetAllByParentDirectoryIdsAsync(notExistingDirectories.Select(static (ned) => ned.Id), [], useCachedResult: false);
                    if (removeFiles.Any())
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
                    _logger.LogInformation($"Cached keys:{Environment.NewLine}{string.Join(Environment.NewLine, keys)}");
                }
                _logger.LogError(ex, ex.Message);
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

            await MediaProcessingService.FillEmptyMetadata(fileEntities, setCheckedForFailed: true);
            await MediaProcessingService.FillEmptyThumbnails(fileEntities, setCheckedForFailed: true);
            var endTime = DateTime.Now;

            if (countBeforeCheck != (fileEntities.Count() + directoryEntities.Count()))
            {
                _logger.LogWarning($"{DateTime.Now} - {objectID} Some object was removed for directory {directoryStartObject?.DirectoryFullPath}. Object in database = {countBeforeCheck}. Objects after check = {fileEntities.Count() + directoryEntities.Count()}");
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                StringBuilder sb = new();
                _ = sb.Append($"{DateTime.Now:dd/MM/yyyy HH:mm:ss:fff}");
                _ = sb.Append($" - ");
                _ = sb.Append($"ObjectID: {objectID}, ");
                _ = sb.Append($"Start: {startTime:HH:mm:ss:fff}, ");
                _ = sb.Append($"Get directory: {getDirectoryTime:HH:mm:ss:fff} ({(getDirectoryTime - startTime).TotalMilliseconds:0.00}ms), ");
                _ = sb.Append($"Get files: {getAllFilesInDirectoryTime:HH:mm:ss:fff} ({(getAllFilesInDirectoryTime - getDirectoryTime).TotalMilliseconds:0.00}ms), ");
                _ = sb.Append($"Refresh found files: {refreshFoundFilesTime:HH:mm:ss:fff} ({(refreshFoundFilesTime - getAllFilesInDirectoryTime).TotalMilliseconds:0.00}ms), ");
                _ = sb.Append($"Get data from database: {getEntitiesTime:HH:mm:ss:fff} ({(getEntitiesTime - refreshFoundFilesTime).TotalMilliseconds:0.00}ms), ");
                _ = sb.Append($"Add additional data from database: {addAdditionalEntitiesTime:HH:mm:ss:fff} ({(addAdditionalEntitiesTime - getEntitiesTime).TotalMilliseconds:0.00}ms), ");
                _ = sb.Append($"Filter data: {filterEntitiesTime:HH:mm:ss:fff} ({(filterEntitiesTime - addAdditionalEntitiesTime).TotalMilliseconds:0.00}ms), ");
                _ = sb.Append($"Check files: {checkEntitiesTime:HH:mm:ss:fff} ({(checkEntitiesTime - filterEntitiesTime).TotalMilliseconds:0.00}ms), ");
                _ = sb.Append($"Fill empty data : {endTime:HH:mm:ss:fff} ({(endTime - checkEntitiesTime).TotalMilliseconds:0.00}ms), ");
                _ = sb.Append($"End: {endTime:HH:mm:ss:fff}, ");
                _ = sb.Append($"Total duration (ms): {(endTime - startTime).TotalMilliseconds:0.00}, ");
                _ = sb.Append($"Directory: {directoryStartObject?.DirectoryFullPath}");
                _logger.LogInformation(sb.ToString());
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

            foreach (var file in files)
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
            foreach (var file in files)
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
