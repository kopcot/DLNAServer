using DLNAServer.Common;
using DLNAServer.Features.Cache.Interfaces;
using DLNAServer.Helpers.Logger;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace DLNAServer.Controllers
{
    /// <summary>
    /// Serve the SCPD (Service Control Protocol Description) XML
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public partial class SCPDController : Controller
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
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(GetResourceFileSCPD),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);

            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "xml", fileName);

            if (!System.IO.File.Exists(filePath))
            {
                WarningFileNotExists(filePath);
                return NotFound("File not found");
            }

            (var isCachedSuccessful, var fileMemoryByteMemory) = await FileMemoryCacheManager.CacheFileAndReturnAsync(filePath, TimeSpanValues.Time1day, checkExistingInCache: true);
            if (isCachedSuccessful)
            {
                var fileContent = Encoding.UTF8.GetString(fileMemoryByteMemory.Span);
                return Content(
                    content: fileContent,
                    contentType: "text/xml; charset=\"utf-8\"",
                    contentEncoding: Encoding.UTF8);
            }

            return PhysicalFile(
                physicalPath: filePath,
                contentType: "text/xml; charset=\"utf-8\"",
                fileDownloadName: fileName,
                enableRangeProcessing: false);
        }
    }
}
