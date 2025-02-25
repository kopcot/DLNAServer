using DLNAServer.Features.Cache.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace DLNAServer.Controllers.Media
{
    [Route("[controller]")]
    [ApiController]
    public class IconController : Controller
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
            try
            {
                FileExtensionContentTypeProvider provider = new();
                if (!provider.TryGetContentType(fileName, out var mimeType))
                {
                    mimeType = "application/octet-stream"; // Default MIME type if unknown
                }

                string filePath = Path.Combine([Directory.GetCurrentDirectory(), "Resources", "images", "icons", fileName]);

                (var isCachedSuccessful, var fileMemoryByteMemory) = await FileMemoryCache.CacheFileAndReturnAsync(filePath, TimeSpan.FromDays(1), checkExistingInCache: true);
                if (isCachedSuccessful
                    && fileMemoryByteMemory != null
                    && fileMemoryByteMemory.HasValue)
                {
                    return File(fileMemoryByteMemory.Value.ToArray(), mimeType, enableRangeProcessing: true);
                }

                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning($"{DateTime.Now}: File not found, {filePath}");
                    return NotFound("File not found");
                }
                return PhysicalFile(filePath, mimeType, enableRangeProcessing: false);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(StatusCodes.Status499ClientClosedRequest, "Client Closed Request"); // Custom status code for client cancellation
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }
    }
}
