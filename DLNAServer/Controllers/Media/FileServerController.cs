using CommunityToolkit.HighPerformance;
using DLNAServer.Common;
using DLNAServer.Configuration;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.Cache.Interfaces;
using DLNAServer.Helpers.Logger;
using DLNAServer.Types.DLNA;
using Microsoft.AspNetCore.Mvc;

namespace DLNAServer.Controllers.Media
{
    [Route("[controller]")]
    [ApiController]
    public partial class FileServerController : Controller
    {
        private readonly ILogger<FileServerController> _logger;
        private readonly ServerConfig _serverConfig;
        private readonly Lazy<IFileRepository> _fileRepositoryLazy;
        private readonly Lazy<IThumbnailRepository> _thumbnailRepositoryLazy;
        private readonly Lazy<IThumbnailDataRepository> _thumbnailDataRepositoryLazy;
        private readonly Lazy<IFileMemoryCacheManager> _fileMemoryCacheLazy;
        private IFileRepository FileRepository => _fileRepositoryLazy.Value;
        private IThumbnailRepository ThumbnailRepository => _thumbnailRepositoryLazy.Value;
        private IThumbnailDataRepository ThumbnailDataRepository => _thumbnailDataRepositoryLazy.Value;
        private IFileMemoryCacheManager FileMemoryCache => _fileMemoryCacheLazy.Value;
        public FileServerController(
            ILogger<FileServerController> logger,
            ServerConfig serverConfig,
            Lazy<IFileRepository> fileRepositoryLazy,
            Lazy<IThumbnailRepository> thumbnailRepositoryLazy,
            Lazy<IThumbnailDataRepository> thumbnailDataRepositoryLazy,
            Lazy<IFileMemoryCacheManager> fileMemoryCacheLazy)
        {
            _logger = logger;
            _serverConfig = serverConfig;
            _fileRepositoryLazy = fileRepositoryLazy;
            _thumbnailRepositoryLazy = thumbnailRepositoryLazy;
            _thumbnailDataRepositoryLazy = thumbnailDataRepositoryLazy;
            _fileMemoryCacheLazy = fileMemoryCacheLazy;
        }
        [HttpGet("file/{fileGuid}")]
        public async Task<IActionResult> GetMediaFileAsync([FromRoute] string fileGuid)
        {
            var file = await FileRepository.GetByIdAsync(fileGuid, asNoTracking: true, useCachedResult: true);
            if (file == null)
            {
                WarningFileNotFound(fileGuid);
                return NotFound($"File with id '{fileGuid}' not found");
            }

            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(GetMediaFileAsync),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);

            return GetMediaFile(ref file);
        }
        [HttpGet("thumbnail/{thumbnailGuid}")]
        public async Task<IActionResult> GetMediaFileThumbnailAsync([FromRoute] string thumbnailGuid)
        {
            var file = await ThumbnailRepository.GetByIdAsync(thumbnailGuid, asNoTracking: true, useCachedResult: true);
            if (file == null)
            {
                WarningFileNotFound(thumbnailGuid);
                return NotFound($"Thumbnail with id '{thumbnailGuid}' not found");
            }

            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(GetMediaFileAsync),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);

            return await GetMediaFileThumbnailAsync(file);
        }
        private IActionResult GetMediaFile(ref FileEntity file)
        {
            try
            {
                var connection = HttpContext.Connection;
                if (!System.IO.File.Exists(file.FilePhysicalFullPath))
                {
                    string message = string.Format("File '{0}' not found", [file.FilePhysicalFullPath]);
                    _logger.LogGeneralErrorMessage(new FileNotFoundException(message: message, fileName: file.FilePhysicalFullPath));
                    return NotFound(message);
                }
                DebugFilePath(file.FilePhysicalFullPath);

                if (_serverConfig.UseMemoryCacheForStreamingFile && !file.FileUnableToCache)
                {
                    (bool isCachedSuccessful, var cachedData) = FileMemoryCache.GetCheckCachedFile(
                        file.FilePhysicalFullPath,
                        TimeSpan.FromMinutes(_serverConfig.StoreFileInMemoryCacheAfterLoadInMinute));
                    if (isCachedSuccessful)
                    {
                        InformationServingFileFromCache(
                            connection?.RemoteIpAddress,
                            connection?.RemotePort,
                            file.FilePhysicalFullPath,
                            file.FileDlnaMime.ToMimeString(),
                            file.FileSizeInBytes / (1024.0 * 1024.0)
                        );
                        return File(cachedData.AsStream(), file.FileDlnaMime.ToMimeString(), enableRangeProcessing: true);
                    }
                    else
                    {
                        FileMemoryCache.CacheFileInBackground(
                            file,
                            TimeSpan.FromMinutes(_serverConfig.StoreFileInMemoryCacheAfterLoadInMinute));
                    }
                }

                InformationServingFileFromDisk(
                    connection?.RemoteIpAddress,
                    connection?.RemotePort,
                    file.FilePhysicalFullPath,
                    file.FileDlnaMime.ToMimeString(),
                    file.FileSizeInBytes / (1024.0 * 1024.0)
                );
                return PhysicalFile(file.FilePhysicalFullPath, file.FileDlnaMime.ToMimeString(), enableRangeProcessing: true);
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.LogWarningOperationCanceled(_logger);
                return StatusCode(StatusCodes.Status499ClientClosedRequest, "Client Closed Request");
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
                return BadRequest(ex);
            }
        }
        private async Task<IActionResult> GetMediaFileThumbnailAsync(ThumbnailEntity thumbnail)
        {
            try
            {
                var connection = HttpContext.Connection;
                DebugThumbnailFilePath(thumbnail.ThumbnailFilePhysicalFullPath);

                if (thumbnail.ThumbnailDataId.HasValue)
                {
                    var thumbnailData = thumbnail.ThumbnailData
                        ?? await ThumbnailDataRepository.GetByIdAsync(thumbnail.ThumbnailDataId.Value, asNoTracking: true, useCachedResult: true);
                    if (thumbnailData != null
                        && thumbnailData.ThumbnailData != null)
                    {
                        InformationServingThumbnailFileFromDatabase(
                            connection?.RemoteIpAddress,
                            connection?.RemotePort,
                            thumbnail.ThumbnailFilePhysicalFullPath,
                            thumbnail.ThumbnailFileDlnaMime.ToMimeString(),
                            thumbnail.ThumbnailFileSizeInBytes / (1024.0)
                        );
                        return File(thumbnailData.ThumbnailData, thumbnail.ThumbnailFileDlnaMime.ToMimeString() ?? string.Empty, enableRangeProcessing: true);
                    }
                }

                if (!System.IO.File.Exists(thumbnail.ThumbnailFilePhysicalFullPath))
                {
                    string message = string.Format("Thumbnail file '{0}' not found", [thumbnail.ThumbnailFilePhysicalFullPath]);
                    _logger.LogGeneralErrorMessage(new FileNotFoundException(message: message, fileName: thumbnail.ThumbnailFilePhysicalFullPath));
                    return NotFound(message);
                }

                if (_serverConfig.UseMemoryCacheForStreamingFile)
                {
                    (var isCachedSuccessful, var fileMemoryByteMemory) = await FileMemoryCache.CacheFileAndReturnAsync(thumbnail.ThumbnailFilePhysicalFullPath, TimeSpanValues.Time1day);
                    if (isCachedSuccessful)
                    {
                        InformationServingThumbnailFileFromCache(
                            connection?.RemoteIpAddress,
                            connection?.RemotePort,
                            thumbnail.ThumbnailFilePhysicalFullPath,
                            thumbnail.ThumbnailFileDlnaMime.ToMimeString(),
                            thumbnail.ThumbnailFileSizeInBytes / (1024.0)
                        );
                        return File(fileMemoryByteMemory.AsStream(), thumbnail.ThumbnailFileDlnaMime.ToMimeString() ?? string.Empty, enableRangeProcessing: true);
                    }

                    DebugUnableToCacheThumbnailFile();
                }

                InformationServingThumbnailFileFromDisk(
                    connection?.RemoteIpAddress,
                    connection?.RemotePort,
                    thumbnail.ThumbnailFilePhysicalFullPath,
                    thumbnail.ThumbnailFileDlnaMime.ToMimeString(),
                    thumbnail.ThumbnailFileSizeInBytes / (1024.0)
                );
                return PhysicalFile(thumbnail.ThumbnailFilePhysicalFullPath, thumbnail.ThumbnailFileDlnaMime.ToMimeString() ?? string.Empty, enableRangeProcessing: true);
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.LogWarningOperationCanceled(_logger);
                return StatusCode(StatusCodes.Status499ClientClosedRequest, "Client Closed Request");
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
                return BadRequest(ex);
            }
        }
    }
}
