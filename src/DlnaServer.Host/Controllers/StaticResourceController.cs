using CommunityToolkit.HighPerformance;
using DlnaServer.Host.Delivery;
using DlnaServer.Host.Delivery.Caching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using DlnaServer.Core.Delivery;

namespace DlnaServer.Host.Controllers
{
    /// <summary>
    /// Serves the static protocol assets: SCPD service descriptions and device icons.
    /// </summary>
    /// <remarks>
    /// Both sets are copied byte-for-byte from the reference, because renderers fetch them by the exact
    /// URLs advertised in <c>description.xml</c>.
    /// <para>
    /// Served out of the served-bytes cache. This is the smallest and hottest of its three content
    /// classes - a few hundred kilobytes in total, and nearly every DIDL-Lite container response points
    /// a renderer at an icon - so it is retained longest and evicted last.
    /// </para>
    /// </remarks>
    [ApiController]
    public sealed partial class StaticResourceController : ControllerBase
    {
        private const string ScpdContentType = "text/xml; charset=\"utf-8\"";
        private const string FallbackIconContentType = "application/octet-stream";

        /// <summary>
        /// The device icon reused as the browser tab icon: 48x48, which is the favicon size.
        /// </summary>
        // One spelling of the icon folder, used by both icon actions.
        private static readonly string IconFolder = Path.Combine("images", "icons");

        private const string FaviconFileName = "small.png";

        private static readonly FileExtensionContentTypeProvider _contentTypes = new();

        private readonly IWebHostEnvironment _environment;
        private readonly IServedFileCache _cache;
        private readonly ILogger<StaticResourceController> _logger;

        public StaticResourceController(
            IWebHostEnvironment environment,
            IServedFileCache cache,
            ILogger<StaticResourceController> logger)
        {
            _environment = environment;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Returns one SCPD document, describing the actions a service supports.
        /// </summary>
        [HttpGet("/SCPD/{fileName}")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> GetServiceDescription(
            [FromRoute] string fileName,
            CancellationToken cancellationToken)
        {
            var path = ResolveResourcePath("xml", fileName);

            if (path is null)
            {
                LogResourceNotFound(fileName);
                return NotFound();
            }

            return await ServeAsync(path, ScpdContentType, cancellationToken);
        }

        /// <summary>
        /// Returns one device, folder or media-kind icon.
        /// </summary>
        /// <remarks>
        /// Two routes for the same files, for the reason <see cref="GetFavicon"/> gives: the admin UI
        /// falls back to these icons for a file with no preview, and
        /// <see cref="AdminOrMediaPortEndpointFilter"/> serves an unprefixed path on the media port only -
        /// so without the second route every such tile is a broken image on the admin port.
        /// </remarks>
        [HttpGet("/icon/{fileName}")]
        [HttpGet("/admin/icon/{fileName}")]
        [ResponseCache(Duration = 86_400, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> GetIcon(
            [FromRoute] string fileName,
            CancellationToken cancellationToken)
        {
            var path = ResolveResourcePath(IconFolder, fileName);

            if (path is null)
            {
                LogResourceNotFound(fileName);
                return NotFound();
            }

            var contentType = _contentTypes.TryGetContentType(path, out var resolved)
                ? resolved
                : FallbackIconContentType;

            return await ServeAsync(path, contentType, cancellationToken);
        }

        /// <summary>
        /// Returns the browser tab icon, which is the server's own 48x48 device icon.
        /// </summary>
        /// <remarks>
        /// Two routes for one file because the two ports are two sites to a browser, and
        /// <see cref="AdminOrMediaPortEndpointFilter"/> admits a path on the media port unless it begins
        /// with <c>/admin</c>. The admin route is what keeps the promise that no admin page ever names the
        /// renderer-facing port.
        /// <para>
        /// The bytes are a PNG whatever the <c>.ico</c> in the path suggests; browsers go by the content
        /// type, and that URL is the one they ask for unprompted.
        /// </para>
        /// </remarks>
        [HttpGet("/favicon.ico")]
        [HttpGet("/admin/favicon.ico")]
        [ResponseCache(Duration = 86_400, Location = ResponseCacheLocation.Client)]
        public Task<IActionResult> GetFavicon(CancellationToken cancellationToken)
        {
            return GetIcon(FaviconFileName, cancellationToken);
        }

        /// <summary>
        /// Serves a resource from memory, reading it in on the first request.
        /// </summary>
        /// <remarks>
        /// A failed cache read is not a failed request: the response falls back to the file on disc,
        /// which is what the endpoint did before the cache existed.
        /// </remarks>
        private async Task<IActionResult> ServeAsync(
            string path,
            string contentType,
            CancellationToken cancellationToken)
        {
            if (_cache.TryGet(path, out var cached))
            {
                return File(cached.AsStream(), contentType);
            }

            var loaded = await _cache.LoadAsync(path, CachedContentClass.StaticAsset, cancellationToken);

            return loaded.IsEmpty
                ? PhysicalFile(path, contentType)
                : File(loaded.AsStream(), contentType);
        }

        /// <summary>
        /// Resolves a file inside a resource folder, or null when it does not exist.
        /// </summary>
        /// <remarks>
        /// The requested name is reduced to its file name before use, so a crafted value such as
        /// <c>..%2f..%2fappsettings.json</c> cannot escape the resource folder.
        /// </remarks>
        private string? ResolveResourcePath(string relativeFolder, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var safeName = Path.GetFileName(fileName);

            if (string.IsNullOrEmpty(safeName))
            {
                return null;
            }

            var root = Path.Combine(_environment.ContentRootPath, "Resources", relativeFolder);
            var candidate = Path.GetFullPath(Path.Combine(root, safeName));

            if (!candidate.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return null;
            }

            return System.IO.File.Exists(candidate) ? candidate : null;
        }
    }
}
