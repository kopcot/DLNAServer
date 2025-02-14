using DLNAServer.Features.Cache.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DLNAServer.Controllers
{
    /// <summary>
    /// Serve the SCPD (Service Control Protocol Description) XML
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public class SCPDController : Controller
    {
        private readonly ILogger<SCPDController> _logger;
        private readonly Lazy<IFileMemoryCacheManager> _fileMemoryCacheLazy;
        private IFileMemoryCacheManager FileMemoryCacheManager => _fileMemoryCacheLazy.Value;
        public SCPDController(
            ILogger<SCPDController> logger,
            Lazy<IFileMemoryCacheManager> fileMemoryCacheLazy)
        {
            _logger = logger;
            _fileMemoryCacheLazy = fileMemoryCacheLazy;
        }
        [HttpGet("{fileName}")]
        public async Task<IActionResult> GetResourceFileSCPD([FromRoute] string fileName)
        {
            _logger.LogDebug($"{nameof(GetResourceFileSCPD)}, {this.HttpContext.Connection.RemoteIpAddress}:{this.HttpContext.Connection.RemotePort}  path: '{this.ControllerContext.HttpContext.Request.Path.Value}',  method: '{this.HttpContext.Request.Method}'");

            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "xml", fileName);

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning($"{DateTime.Now}: File not found, {filePath}");
                return NotFound("File not found");
            }

            (var isCachedSuccessful, var fileMemoryByteWR) = await FileMemoryCacheManager.CacheFileAndReturnAsync(filePath, TimeSpan.FromDays(1), checkExistingInCache: true);
            if (isCachedSuccessful
                && fileMemoryByteWR != null
                && fileMemoryByteWR.TryGetTarget(out var fileMemoryByte) == true)
            {
                return File(fileMemoryByte!, "text/xml; charset=\"utf-8\"");
            }

            return PhysicalFile(filePath, "text/xml; charset=\"utf-8\"");
        }
    }
}
