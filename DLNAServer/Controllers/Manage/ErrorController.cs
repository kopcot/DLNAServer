using DLNAServer.Helpers.Attributes;
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
            _logger.LogWarning($"{nameof(Handle404Get)} - path: '{ControllerContext.HttpContext.Request.Path.Value}',  method: '{HttpContext.Request.Method}'");
            return NotFoundFallback();
        }

        [HttpPost]
        public IActionResult Handle404Post()
        {
            _logger.LogWarning($"{nameof(Handle404Post)} - path: '{ControllerContext.HttpContext.Request.Path.Value}',  method: '{HttpContext.Request.Method}'");
            return NotFoundFallback();
        }

        [HttpPut]
        public IActionResult Handle404Put()
        {
            _logger.LogWarning($"{nameof(Handle404Put)} - path: '{ControllerContext.HttpContext.Request.Path.Value}',  method: '{HttpContext.Request.Method}'");
            return NotFoundFallback();
        }

        [HttpDelete]
        public IActionResult Handle404Delete()
        {
            _logger.LogWarning($"{nameof(Handle404Delete)} - path: '{ControllerContext.HttpContext.Request.Path.Value}',  method: '{HttpContext.Request.Method}'");
            return NotFoundFallback();
        }
        [HttpSubscribe]
        public IActionResult Handle404Subscribe()
        {
            _logger.LogWarning($"{nameof(Handle404Delete)} - path: '{ControllerContext.HttpContext.Request.Path.Value}',  method: '{HttpContext.Request.Method}'");
            return NotFoundFallback();
        }
        [Route("[controller]/NotFoundFallback")]
        public IActionResult NotFoundFallback()
        {
            _logger.LogWarning($"{nameof(NotFoundFallback)} - path: '{ControllerContext.HttpContext.Request.Path.Value}',  method: '{HttpContext.Request.Method}'");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
        [Route("[controller]/Error")]
        public IActionResult HandleError()
        {
            _logger.LogWarning($"{nameof(NotFoundFallback)} - path: '{ControllerContext.HttpContext.Request.Path.Value}',  method: '{HttpContext.Request.Method}'");
            return Problem();
        }
    }
}
