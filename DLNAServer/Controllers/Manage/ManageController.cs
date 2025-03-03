using DLNAServer.Configuration;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.ApiBlocking.Interfaces;
using DLNAServer.Features.FileWatcher.Interfaces;
using DLNAServer.Features.MediaContent.Interfaces;
using DLNAServer.Features.MediaProcessors.Interfaces;
using DLNAServer.Features.Subscriptions.Data;
using DLNAServer.Helpers.Diagnostics;
using DLNAServer.Helpers.Logger;
using DLNAServer.Types.DLNA;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Buffers;
using System.Runtime;

namespace DLNAServer.Controllers.Manage
{
    [Route("[controller]")]
    [ApiController]
    public partial class ManageController : Controller
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ManageController> _logger;
        private readonly ServerConfig _serverConfig;
        private readonly Lazy<IFileRepository> _fileRepositoryLazy;
        private readonly Lazy<IDirectoryRepository> _directoryRepositoryLazy;
        private readonly Lazy<IVideoMetadataRepository> _videoMetadataRepositoryLazy;
        private readonly Lazy<IThumbnailRepository> _thumbnailRepositoryLazy;
        private readonly Lazy<IThumbnailDataRepository> _thumbnailDataRepositoryLazy;
        private readonly Lazy<IContentExplorerManager> _contentExplorerManagerLazy;
        private readonly Lazy<IMediaProcessingService> _mediaProcessingServiceLazy;
        private readonly Lazy<IApiBlockerService> _apiBlockerServiceLazy;
        private readonly Lazy<IMemoryCache> _memoryCacheLazy;
        private readonly Lazy<IFileWatcherHandler> _fileWatcherHandlerLazy;

        private IFileRepository FileRepository => _fileRepositoryLazy.Value;
        private IDirectoryRepository DirectoryRepository => _directoryRepositoryLazy.Value;
        private IVideoMetadataRepository VideoMetadataRepository => _videoMetadataRepositoryLazy.Value;
        private IThumbnailRepository ThumbnailRepository => _thumbnailRepositoryLazy.Value;
        private IThumbnailDataRepository ThumbnailDataRepository => _thumbnailDataRepositoryLazy.Value;
        private IContentExplorerManager ContentExplorerManager => _contentExplorerManagerLazy.Value;
        private IMediaProcessingService MediaProcessingService => _mediaProcessingServiceLazy.Value;
        private IApiBlockerService ApiBlockerService => _apiBlockerServiceLazy.Value;
        private IMemoryCache MemoryCache => _memoryCacheLazy.Value;
        private IFileWatcherHandler FileWatcherHandler => _fileWatcherHandlerLazy.Value;
        public ManageController(
            IServiceScopeFactory serviceScopeFactory,
            ServerConfig serverConfig,
            Lazy<IFileRepository> fileRepositoryLazy,
            Lazy<IDirectoryRepository> directoryRepositoryLazy,
            Lazy<IVideoMetadataRepository> videoMetadataRepositoryLazy,
            Lazy<IThumbnailRepository> thumbnailRepositoryLazy,
            Lazy<IThumbnailDataRepository> thumbnailDataRepositoryLazy,
            Lazy<IContentExplorerManager> contentExplorerManagerLazy,
            Lazy<IMediaProcessingService> mediaProcessingServiceLazy,
            Lazy<IApiBlockerService> apiBlockerServiceLazy,
            Lazy<IMemoryCache> memoryCacheLazy,
            Lazy<IFileWatcherHandler> fileWatcherHandlerLazy,
            ILogger<ManageController> logger)
        {
            _serverConfig = serverConfig;
            _serviceScopeFactory = serviceScopeFactory;
            _fileRepositoryLazy = fileRepositoryLazy;
            _directoryRepositoryLazy = directoryRepositoryLazy;
            _videoMetadataRepositoryLazy = videoMetadataRepositoryLazy;
            _thumbnailRepositoryLazy = thumbnailRepositoryLazy;
            _thumbnailDataRepositoryLazy = thumbnailDataRepositoryLazy;
            _contentExplorerManagerLazy = contentExplorerManagerLazy;
            _mediaProcessingServiceLazy = mediaProcessingServiceLazy;
            _apiBlockerServiceLazy = apiBlockerServiceLazy;
            _memoryCacheLazy = memoryCacheLazy;
            _fileWatcherHandlerLazy = fileWatcherHandlerLazy;
            _logger = logger;
        }
        [HttpGet("configuration")]
        public async Task<ActionResult<ServerConfig>> GetServerConfigAsync()
        {
            await Task.CompletedTask;
            return Ok(_serverConfig);
        }
        [HttpGet("database")]
        public async Task<ActionResult<IEnumerable<FileEntity>>> GetRowCountsAsync()
        {
            var rowCounts = await FileRepository.DbContext.GetAllTablesRowCountAsync();

            return Ok(rowCounts);
        }
        [HttpGet("file")]
        public async Task<ActionResult<IEnumerable<FileEntity>>> GetAllFilesAsync()
        {
            var files = await FileRepository.GetAllAsync(asNoTracking: true, useCachedResult: false);

            return Ok(files);
        }
        [HttpGet("file/{guid}")]
        public async Task<ActionResult<FileEntity>> GetFileByIdAsync([FromRoute] string guid)
        {
            var files = await FileRepository.GetByIdAsync(guid, asNoTracking: true, useCachedResult: false);

            return Ok(files);
        }
        [HttpGet("fileLast")]
        public async Task<ActionResult<IEnumerable<FileEntity>>> GetLastFilesAsync()
        {
            var files = await FileRepository.GetAllByAddedToDbAsync((int)_serverConfig.CountOfFilesByLastAddedToDb, _serverConfig.ExcludeFolders, useCachedResult: false);

            return Ok(files);
        }
        [HttpGet("directory")]
        public async Task<ActionResult<IEnumerable<DirectoryEntity>>> GetAllDirectoriesAsync()
        {
            var directories = await DirectoryRepository.GetAllAsync(asNoTracking: true, useCachedResult: false);

            return Ok(directories);
        }
        [HttpGet("directory/{guid}")]
        public async Task<ActionResult<DirectoryEntity>> GetDirectoryByIdAsync([FromRoute] string guid)
        {
            var directories = await DirectoryRepository.GetByIdAsync(guid, asNoTracking: true, useCachedResult: false);

            return Ok(directories);
        }
        [HttpGet("videoMetadata")]
        public async Task<ActionResult<IEnumerable<MediaVideoEntity>>> GetAllVideoMetadataAsync()
        {
            var videoMetadata = await VideoMetadataRepository.GetAllAsync(asNoTracking: true, useCachedResult: false);

            return Ok(videoMetadata);
        }
        [HttpGet("thumbnail")]
        public async Task<ActionResult<IEnumerable<ThumbnailEntity>>> GetAllThumbnailAsync()
        {
            var thumbnails = await ThumbnailRepository.GetAllAsync(asNoTracking: true, useCachedResult: false);

            return Ok(thumbnails);
        }
        [HttpGet("thumbnail/{guid}")]
        public async Task<ActionResult<ThumbnailEntity>> GetThumbnailByGuidAsync([FromRoute] string guid)
        {
            var thumbnail = await ThumbnailRepository.GetByIdAsync(guid, asNoTracking: true, useCachedResult: false);

            return Ok(thumbnail);
        }
        [HttpGet("thumbnailData")]
        public async Task<ActionResult<IEnumerable<ThumbnailDataEntity>>> GetAllThumbnailDataAsync()
        {
            var thumbnailData = await ThumbnailDataRepository.GetAllAsync(asNoTracking: true, useCachedResult: false);

            return Ok(thumbnailData);
        }
        [HttpGet("dlnaMime")]
        public async Task<IActionResult> GetAllDlnaMimes()
        {
            var dlnaMimes = Enum.GetValues(typeof(DlnaMime)).Cast<DlnaMime>();

            var result = dlnaMimes?.Select(static (m) =>
            {
                try
                {
                    return new
                    {
                        Id = (int)m,
                        DlnaMime = $"{m}",
                        ContentType = m.ToMimeString(),
                        MimeDescription = m.ToMimeDescription(),
                        DlnaMedia = $"{m.ToDlnaMedia()}",
                        DefaultDlnaItemClass = $"{m.ToDefaultDlnaItemClass()}",
                        MainProfileName = m.ToMainProfileNameString() ?? string.Empty,
                        ProfileNames = m.ToProfileNameString(),
                        Extensions = m.DefaultFileExtensions(),
                        IsError = false,
                        Error = string.Empty
                    };
                }
                catch (Exception ex)
                {
                    return new
                    {
                        Id = (int)m,
                        DlnaMime = $"{m}",
                        ContentType = string.Empty,
                        MimeDescription = string.Empty,
                        DlnaMedia = string.Empty,
                        DefaultDlnaItemClass = string.Empty,
                        MainProfileName = string.Empty,
                        ProfileNames = Array.Empty<string>(),
                        Extensions = Array.Empty<string>(),
                        IsError = true,
                        Error = ex.Message
                    };
                }
            }
            ).ToList() ?? [];

            await Task.CompletedTask;
            return Ok(result);
        }
        [HttpGet("subscriptions")]
        public async Task<ActionResult<IEnumerable<Subscription>>> GetAllSubscriptionAsync()
        {

            await Task.CompletedTask;
            return Ok();
        }
        [HttpGet("memoryCache")]
        public async Task<IActionResult> GetMemoryCacheInfo()
        {
            Dictionary<string, string?> memoryCacheInfo = [];
            if (MemoryCache is MemoryCache memoryCache)
            {
                memoryCacheInfo.Add("Count", $"{memoryCache.Count}");
                var keys = memoryCache
                    .Keys
                    .OrderBy(static (k) => k)
                    .Select(static (k, i) => new KeyValuePair<string, string?>($"Key_{i}", $"{k}"))
                    .ToList();
                foreach (var key in keys)
                {
                    memoryCacheInfo.Add(key.Key, key.Value);
                }
            }
            await Task.CompletedTask;
            return Ok(memoryCacheInfo);
        }
        [HttpGet("memoryCacheClear")]
        public async Task<IActionResult> GetMemoryCacheClear()
        {
            if (MemoryCache is MemoryCache memoryCache)
            {
                try
                {
                    foreach (var key in memoryCache.Keys.ToList())
                    {
                        MemoryCache.Remove(key);
                    }
                }
                catch (Exception ex)
                {
                    memoryCache.Clear();

                    _logger.LogGeneralErrorMessage(ex);
                }

                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect();

                return Ok($"Cleared. Actual count: {memoryCache.Count}");
            }


            await Task.CompletedTask;
            return BadRequest($"MemoryCache is not {typeof(MemoryCache).FullName}");

        }
        [HttpGet("stop")]
        public IActionResult GetExitApplication()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var hostApplicationLifetime = scope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>();
                hostApplicationLifetime.StopApplication();
            }
            return Ok("stopping");
        }
        [HttpGet("restart")]
        public IActionResult GetRestartApplication()
        {
            ServerConfig.DlnaServerRestart = true;

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var hostApplicationLifetime = scope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>();

                hostApplicationLifetime.StopApplication();

                return Ok("restarting");
            }
        }
        [HttpGet("clearAllMetadata")]
        public async Task<IActionResult> GetClearAllMetadataAsync()
        {
            await ContentExplorerManager.ClearAllMetadataAsync();

            return Ok("cleared");
        }
        [HttpGet("clearAllThumbnails")]
        public async Task<IActionResult> GetClearAllThumbnailsAsync()
        {
            await ContentExplorerManager.ClearAllThumbnailsAsync();

            return Ok("cleared");
        }
        private static bool IsGetRecreateAllFilesInfoAsyncActive;
        [HttpGet("recreateAllFilesInfo")]
        public async Task<IActionResult> GetRecreateAllFilesInfoAsync()
        {
            if (IsGetRecreateAllFilesInfoAsyncActive)
            {
                return Ok("Recreating is in progress from another request.");
            }
            IsGetRecreateAllFilesInfoAsyncActive = true;
            ApiBlockerService.BlockApi(true, $"Recreate all file info");


            try
            {
                using (ScopeRecreatingFilesInfo(_logger))
                {
                    FileWatcherHandler.EnableRaisingEvents(false);

                    {
                        using (var scope = _serviceScopeFactory.CreateScope())
                        {
                            var contentExplorerManager = scope.ServiceProvider.GetRequiredService<IContentExplorerManager>();

                            var directoryRepository = scope.ServiceProvider.GetRequiredService<IDirectoryRepository>();
                            DirectoryEntity[] directories = await directoryRepository.GetAllAsync(useCachedResult: false);
                            _ = await contentExplorerManager.CheckDirectoriesExistingAsync(directories);

                            var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
                            FileEntity[] files = await fileRepository.GetAllAsync(useCachedResult: false);
                            _ = await contentExplorerManager.CheckFilesExistingAsync(files);
                        }

                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                    }

                    const int maxChunkSize = 50;
                    long fileCountAll;
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
                        fileCountAll = await fileRepository.GetCountAsync();
                    }
                    int chunksCount = (int)Math.Round((double)fileCountAll / maxChunkSize, 0, MidpointRounding.ToPositiveInfinity);

                    for (int chunkIndex = 0; chunkIndex < chunksCount; chunkIndex++)
                    {
                        Guid[] filesId;
                        FileEntity[] files;

                        InformationStartRefreshingInfoChunk(chunkIndex + 1, chunksCount, fileCountAll);

                        ApiBlockerService.BlockApi(true, string.Format("Recreate all file info. Progress {0} from {1}.", [chunkIndex + 1, chunksCount]));

                        using (ScopeClearingInfoChunk(_logger))
                        using (var scope = _serviceScopeFactory.CreateScope())
                        {
                            var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
                            var contentExplorerManager = scope.ServiceProvider.GetRequiredService<IContentExplorerManager>();

                            DebugStartClearigInfoChunk(chunkIndex + 1, chunksCount, fileCountAll);

                            files = (await fileRepository.GetAllAsync(chunkIndex * maxChunkSize, maxChunkSize, useCachedResult: false)).ToArray();

                            filesId = files.Select(static (f) => f.Id).ToArray();

                            var task1 = contentExplorerManager.ClearMetadataAsync(files);
                            var task2 = contentExplorerManager.ClearThumbnailsAsync(files);
                            await Task.WhenAll([task1, task2]);

                            DebugDoneClearingInfoChunk(chunkIndex + 1, chunksCount);
                        }

                        using (ScopeRecreatingFilesInfoChunk(_logger))
                        using (var scope = _serviceScopeFactory.CreateScope())
                        {
                            var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
                            var mediaProcessingService = scope.ServiceProvider.GetRequiredService<IMediaProcessingService>();

                            DebugStartRecreatingInfoChunk(chunkIndex + 1, chunksCount, fileCountAll);

                            files = (await fileRepository.GetAllByIdsAsync(filesId, useCachedResult: false)).ToArray();

                            await mediaProcessingService.FillEmptyMetadataAsync(files, setCheckedForFailed: false);
                            await mediaProcessingService.FillEmptyThumbnailsAsync(files, setCheckedForFailed: false);

                            DebugDoneRecreatingInfoChunk(chunkIndex + 1, chunksCount);
                        }

                        await Task.Delay(1000); // 1sec delay for cool down hardware system resources

                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                    }

                    InformationDoneRecreatingInfo();

                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

                    await ContentExplorerManager.InitializeAsync();
                    ApiBlockerService.BlockApi(false);

                    return Ok("recreated");
                }
            }
            catch (Exception ex)
            {
                ApiBlockerService.BlockApi(false);
                return BadRequest(ex.Message);
            }
            finally
            {
                FileWatcherHandler.EnableRaisingEvents(true);
                IsGetRecreateAllFilesInfoAsyncActive = false;
            }
        }
        [HttpGet("recreateFilesInfo/{guid}")]
        public async Task<IActionResult> GetRecreateFilesInfoAsync([FromRoute] string guid)
        {
            var file = await FileRepository.GetByIdAsync(guid, false);
            if (file == null)
            {
                return BadRequest("File not found");
            }

            await ContentExplorerManager.ClearMetadataAsync([file]);
            await ContentExplorerManager.ClearThumbnailsAsync([file]);

            await MediaProcessingService.FillEmptyMetadataAsync([file], setCheckedForFailed: false);
            await MediaProcessingService.FillEmptyThumbnailsAsync([file], setCheckedForFailed: false);

            file = await FileRepository.GetByIdAsync(guid, false);

            return Ok(file);
        }
        [HttpGet("memory")]
        public IActionResult GetMemoryInfo()
        {
            var data = MemoryInfo.ProcessMemoryInfo();

            return Ok(data);
        }
    }
}
