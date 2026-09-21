using DlnaServer.Core.Configuration;
using DlnaServer.Upnp.Description;
using DlnaServer.Upnp.Ssdp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Controllers
{
    /// <summary>
    /// Serves the UPnP device description.
    /// </summary>
    /// <remarks>
    /// This is the first thing a renderer fetches after seeing an SSDP announcement, and the URL it
    /// fetches is the <c>LOCATION</c> header from that announcement.
    /// </remarks>
    [ApiController]
    public sealed class MediaController : ControllerBase
    {
        /// <summary>
        /// Content type the reference sends, quotes around the charset included.
        /// </summary>
        private const string DescriptionContentType = "text/xml; charset=\"utf-8\"";

        private readonly IUpnpDeviceRegistry _devices;
        private readonly IOptionsMonitor<DlnaOptions> _options;

        public MediaController(IUpnpDeviceRegistry devices, IOptionsMonitor<DlnaOptions> options)
        {
            _devices = devices;
            _options = options;
        }

        /// <summary>
        /// Returns <c>description.xml</c>.
        /// </summary>
        /// <remarks>
        /// Also mapped at the site root, because some renderers fetch <c>/</c> rather than the advertised
        /// LOCATION. The <c>uuid</c> query parameter identifies which interface identity is being asked
        /// about; an unknown or absent value falls back to the identity for the address the request
        /// arrived on.
        /// </remarks>
        [HttpGet("/")]
        [HttpGet("/media/description.xml")]

        // No VaryByQueryKeys. It applies ONLY to the server-side response cache middleware, and it throws
        // InvalidOperationException at request time if that middleware is not in the pipeline - which took
        // every Browse on the media port to a 500 the moment the middleware was removed, while the admin
        // port carried on working. The middleware could never cache anything here anyway: Location.Client
        // emits Cache-Control private, and a client keys its own cache on the whole URL, query string
        // included, so varying by `uuid` was already implicit and this attribute bought nothing.
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
        public IActionResult GetDescription([FromQuery] Guid? uuid)
        {
            var identity = ResolveIdentity(uuid);
            var server = _options.CurrentValue.Server;

            var xml = DeviceDescriptionBuilder.Build(new DeviceDescription(
                identity.DeviceId,
                server.FriendlyName,
                server.ManufacturerName,
                server.ManufacturerUrl,
                server.ModelName));

            return Content(xml, DescriptionContentType);
        }

        private UpnpDeviceIdentity ResolveIdentity(Guid? uuid)
        {
            if (uuid is Guid requested)
            {
                foreach (var candidate in _devices.Identities)
                {
                    if (candidate.DeviceId == requested)
                    {
                        return candidate;
                    }
                }
            }

            return _devices.Resolve(HttpContext.Connection.LocalIpAddress);
        }
    }
}
