using DLNAServer.Configuration;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.Cache.Interfaces;
using DLNAServer.Types.DLNA;
using Microsoft.AspNetCore.Mvc;

namespace DLNAServer.Controllers.Media
{
    [Route("[controller]")]
    [ApiController]
    public class FileServerController : Controller
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
                _logger.LogWarning($"{DateTime.Now} File with id '{fileGuid}' not found");
                return NotFound($"File with id '{fileGuid}' not found");
            }

            var connection = HttpContext.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection.RemoteIpAddress}:{connection.RemotePort} , Local IP Address: {connection.LocalIpAddress}:{connection.LocalPort}");

            return await GetMediaFileAsync(file);
        }
        [HttpGet("thumbnail/{thumbnailGuid}")]
        public async Task<IActionResult> GetMediaFileThumbnailAsync([FromRoute] string thumbnailGuid)
        {
            var file = await ThumbnailRepository.GetByIdAsync(thumbnailGuid, asNoTracking: true, useCachedResult: true);
            if (file == null)
            {
                _logger.LogWarning($"{DateTime.Now} Thumbnail with id '{thumbnailGuid}' not found");
                return NotFound($"Thumbnail with id '{thumbnailGuid}' not found");
            }

            var connection = HttpContext.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection.RemoteIpAddress}:{connection.RemotePort} , Local IP Address: {connection.LocalIpAddress}:{connection.LocalPort}");

            return await GetMediaFileThumbnailAsync(file);
        }
        private async Task<IActionResult> GetMediaFileAsync(FileEntity file)
        {
            try
            {
                var connection = HttpContext.Connection;
                if (!System.IO.File.Exists(file.FilePhysicalFullPath))
                {
                    string message = $"File '{file.FilePhysicalFullPath}' not found";
                    _logger.LogError(new FileNotFoundException(message: message, fileName: file.FilePhysicalFullPath), message, [file.FilePhysicalFullPath]);
                    return NotFound($"File '{file.FilePhysicalFullPath}' not found");
                }
                _logger.LogDebug($"File path: {file.FilePhysicalFullPath}");

                if (_serverConfig.UseMemoryCacheForStreamingFile && !file.FileUnableToCache)
                {
                    (bool isCachedSuccessful, var cachedData) = await FileMemoryCache.GetCheckCachedFileAsync(file.FilePhysicalFullPath);
                    if (isCachedSuccessful)
                    {
                        _logger.LogInformation($"{DateTime.Now}: Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort}, Serving file from cache = {file.FilePhysicalFullPath}, {file.FileDlnaMime.ToMimeString()} , {(double)file.FileSizeInBytes / (1024 * 1024):0.00}MB");
                        return File(cachedData, file.FileDlnaMime.ToMimeString(), enableRangeProcessing: true);
                    }
                    else
                    {
                        FileMemoryCache.CacheFileInBackground(
                            file,
                            TimeSpan.FromMinutes(_serverConfig.StoreFileInMemoryCacheAfterLoadInMinute));
                    }
                }

                _logger.LogInformation($"{DateTime.Now}: Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort}, Serving file from disk = {file.FilePhysicalFullPath}, {file.FileDlnaMime.ToMimeString()} , {(double)file.FileSizeInBytes / (1024 * 1024):0.00}MB");
                return PhysicalFile(file.FilePhysicalFullPath, file.FileDlnaMime.ToMimeString(), enableRangeProcessing: true);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Request was canceled by the user");
                return StatusCode(StatusCodes.Status499ClientClosedRequest, "Client Closed Request");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception  {ex.Message}{Environment.NewLine}{ex.StackTrace}", [file.FilePhysicalFullPath]);
                return BadRequest(ex);
            }
        }
        private async Task<IActionResult> GetMediaFileThumbnailAsync(ThumbnailEntity thumbnail)
        {
            try
            {
                var connection = HttpContext.Connection;
                _logger.LogDebug($"Thumbnail file path: {thumbnail.ThumbnailFilePhysicalFullPath}");

                if (thumbnail.ThumbnailDataId.HasValue)
                {
                    var thumbnailData = thumbnail.ThumbnailData ?? await ThumbnailDataRepository.GetByIdAsync(thumbnail.ThumbnailDataId.Value, asNoTracking: true, useCachedResult: true);
                    if (thumbnailData != null
                        && thumbnailData.ThumbnailData != null)
                    {
                        _logger.LogInformation($"{DateTime.Now}: Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort}, Serving thumbnail file from database = {thumbnail.ThumbnailFilePhysicalFullPath}, {thumbnail.ThumbnailFileDlnaMime.ToMimeString()} , {(double)thumbnail.ThumbnailFileSizeInBytes / (1024):0.00}kB");
                        return File(thumbnailData.ThumbnailData, thumbnail.ThumbnailFileDlnaMime.ToMimeString() ?? string.Empty, enableRangeProcessing: true);
                    }
                }

                if (!System.IO.File.Exists(thumbnail.ThumbnailFilePhysicalFullPath))
                {
                    string message = $"Thumbnail '{thumbnail.ThumbnailFilePhysicalFullPath}' not found";
                    _logger.LogError(new FileNotFoundException(message: message, fileName: thumbnail.ThumbnailFilePhysicalFullPath), message, [thumbnail.ThumbnailFilePhysicalFullPath]);
                    return NotFound($"Thumbnail '{thumbnail.ThumbnailFilePhysicalFullPath}' not found");
                }

                if (_serverConfig.UseMemoryCacheForStreamingFile)
                {
                    (var isCachedSuccessful, var fileMemoryByteWR) = await FileMemoryCache.CacheFileAndReturnAsync(thumbnail.ThumbnailFilePhysicalFullPath, TimeSpan.FromDays(1));
                    if (isCachedSuccessful
                        && fileMemoryByteWR != null
                        && fileMemoryByteWR.TryGetTarget(out var fileMemoryByte) == true)
                    {
                        _logger.LogInformation($"{DateTime.Now}: Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort}, Serving thumbnail file from cache = {thumbnail.ThumbnailFilePhysicalFullPath}, {thumbnail.ThumbnailFileDlnaMime.ToMimeString()} , {(double)thumbnail.ThumbnailFileSizeInBytes / (1024):0.00}kB");
                        return File(fileMemoryByte!, thumbnail.ThumbnailFileDlnaMime.ToMimeString() ?? string.Empty, enableRangeProcessing: true);
                    }

                    _logger.LogDebug($"{DateTime.Now}: Unable to cache thumbnail file, serving from disk.");
                }

                _logger.LogInformation($"{DateTime.Now}: Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort}, Serving thumbnail file from disk = {thumbnail.ThumbnailFilePhysicalFullPath}, {thumbnail.ThumbnailFileDlnaMime.ToMimeString()} , {(double)thumbnail.ThumbnailFileSizeInBytes / (1024):0.00}kB");
                return PhysicalFile(thumbnail.ThumbnailFilePhysicalFullPath, thumbnail.ThumbnailFileDlnaMime.ToMimeString() ?? string.Empty, enableRangeProcessing: true);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Request was canceled by the user");
                return StatusCode(StatusCodes.Status499ClientClosedRequest, "Client Closed Request");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception  {ex.Message}{Environment.NewLine}{ex.StackTrace}", [thumbnail.ThumbnailFilePhysicalFullPath]);
                return BadRequest(ex);
            }
        }
    }
}
