using CommunityToolkit.HighPerformance;
using DLNAServer.Common;
using DLNAServer.Features.Cache.Interfaces;
using DLNAServer.Helpers.Logger;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace DLNAServer.Controllers.Media
{
    [Route("[controller]")]
    [ApiController]
    public partial class IconController : Controller
    {
        private readonly ILogger<IconController> _logger;
        private readonly Lazy<IFileMemoryCacheManager> _fileMemoryCacheLazy;
        private IFileMemoryCacheManager FileMemoryCache => _fileMemoryCacheLazy.Value;
        public IconController(
            ILogger<IconController> logger,
            Lazy<IFileMemoryCacheManager> fileMemoryCacheLazy)
        {
            _logger = logger;
            _fileMemoryCacheLazy = fileMemoryCacheLazy;
        }
        [HttpGet("{fileName}")]
        public async Task<IActionResult> GetIconFile(string fileName)
        {
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(GetIconFile),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);
            try
            {
                FileExtensionContentTypeProvider provider = new();
                if (!provider.TryGetContentType(fileName, out var mimeType))
                {
                    mimeType = "application/octet-stream"; // Default MIME type if unknown
                }

                string filePath = Path.Combine([Directory.GetCurrentDirectory(), "Resources", "images", "icons", fileName]);

                (var isCachedSuccessful, var fileMemoryByteMemory) = await FileMemoryCache.CacheFileAndReturnAsync(filePath, TimeSpanValues.Time1day, checkExistingInCache: true);
                if (isCachedSuccessful)
                {
                    return File(fileMemoryByteMemory.AsStream(), mimeType, enableRangeProcessing: true);
                }

                if (!System.IO.File.Exists(filePath))
                {
                    WarningFileNotFound(filePath);
                    return NotFound("File not found");
                }
                return PhysicalFile(filePath, mimeType, enableRangeProcessing: false);
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.LogWarningOperationCanceled(_logger);
                return StatusCode(StatusCodes.Status499ClientClosedRequest, "Client Closed Request"); // Custom status code for client cancellation
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }
    }
}
