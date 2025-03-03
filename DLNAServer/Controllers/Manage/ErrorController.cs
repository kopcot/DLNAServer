using DLNAServer.Helpers.Attributes;
using DLNAServer.Helpers.Logger;
using Microsoft.AspNetCore.Mvc;

namespace DLNAServer.Controllers.Manage
{
    [Route("[controller]")]
    [ApiController]
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;
        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }
        [HttpGet]
        public IActionResult Handle404Get()
        {
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(Handle404Get),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);
            return NotFoundFallback();
        }

        [HttpPost]
        public IActionResult Handle404Post()
        {
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(Handle404Post),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);
            return NotFoundFallback();
        }

        [HttpPut]
        public IActionResult Handle404Put()
        {
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(Handle404Put),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);
            return NotFoundFallback();
        }

        [HttpDelete]
        public IActionResult Handle404Delete()
        {
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(Handle404Delete),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);
            return NotFoundFallback();
        }
        [HttpSubscribe]
        public IActionResult Handle404Subscribe()
        {
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(Handle404Subscribe),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);
            return NotFoundFallback();
        }
        [Route("[controller]/NotFoundFallback")]
        public IActionResult NotFoundFallback()
        {
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(NotFoundFallback),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
        [Route("[controller]/Error")]
        public IActionResult HandleError()
        {
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(HandleError),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);
            return Problem();
        }
    }
}
