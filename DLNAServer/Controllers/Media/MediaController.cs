using DLNAServer.Configuration;
using DLNAServer.Helpers.Logger;
using DLNAServer.SOAP.Constants;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml;

namespace DLNAServer.Controllers.Media
{
    [Route("[controller]")]
    [ApiController]
    public class MediaController : Controller
    {
        private readonly ILogger<MediaController> _logger;
        private readonly ServerConfig _serverConfig;
        public MediaController(
            ILogger<MediaController> logger,
            ServerConfig serverConfig)
        {
            _logger = logger;
            _serverConfig = serverConfig;
        }
        [HttpGet("description.xml")]
        public IActionResult GetDescription([FromQuery] string uuid)
        {
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(GetDescription),
                this.HttpContext.Connection.RemoteIpAddress,
                this.HttpContext.Connection.RemotePort,
                this.HttpContext.Connection.LocalIpAddress,
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Request.Path.Value,
                this.HttpContext.Request.Method);

            string xmlContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<root xmlns=""urn:schemas-upnp-org:device-1-0""  xmlns:dlna=""urn:schemas-dlna-org:device-1-0"" xmlns:sec=""http://www.sec.co.kr/dlna"">
	<specVersion>
		<major>1</major>
		<minor>0</minor>
	</specVersion>
	<device>
		<dlna:X_DLNACAP/>
		<dlna:X_DLNADOC>DMS-1.50</dlna:X_DLNADOC>
		<dlna:X_DLNADOC>M-DMS-1.50</dlna:X_DLNADOC>
		<deviceType>urn:schemas-upnp-org:device:MediaServer:1</deviceType>
		<friendlyName>{_serverConfig.ServerFriendlyName}</friendlyName>
		<manufacturer>{_serverConfig.DlnaServerManufacturerName}</manufacturer>
		<manufacturerURL>{_serverConfig.DlnaServerManufacturerUrl}</manufacturerURL>
		<modelName>{_serverConfig.ServerModelName}</modelName>
		<UDN>uuid:{uuid}</UDN> 
		<modelURL/>
		<modelDescription/>
		<modelNumber/>
		<serialNumber/>
		<sec:ProductCap>smi,DCM10,getMediaInfo.sec,getCaptionInfo.sec</sec:ProductCap>
		<sec:X_ProductCap>smi,DCM10,getMediaInfo.sec,getCaptionInfo.sec</sec:X_ProductCap>
		<iconList>
			<icon>
				<mimetype>image/jpeg</mimetype>
				<width>500</width>
				<height>500</height>
				<depth>24</depth>
				<url>/icon/extraLarge.jpg</url>
			</icon>
			<icon>
				<mimetype>image/png</mimetype>
				<width>500</width>
				<height>500</height>
				<depth>24</depth>
				<url>/icon/extraLarge.png</url>
			</icon>
			<icon>
				<mimetype>image/jpeg</mimetype>
				<width>120</width>
				<height>120</height>
				<depth>24</depth>
				<url>/icon/large.jpg</url>
			</icon>
			<icon>
				<mimetype>image/png</mimetype>
				<width>120</width>
				<height>120</height>
				<depth>24</depth>
				<url>/icon/large.png</url>
			</icon>
			<icon>
				<mimetype>image/jpeg</mimetype>
				<width>48</width>
				<height>48</height>
				<depth>24</depth>
				<url>/icon/small.jpg</url>
			</icon>
			<icon>
				<mimetype>image/png</mimetype>
				<width>48</width>
				<height>48</height>
				<depth>24</depth>
				<url>/icon/small.png</url>
			</icon>
		</iconList>
		<serviceList>
			<service>
				<serviceType>{Services.ServiceType.ConnectionManager}</serviceType>
				<serviceId>{Services.ServiceId.ConnectionManager}</serviceId>
				<eventSubURL>/event/eventAction/ConnectionManager</eventSubURL>
				<controlURL>{EndpointServices.ConnectionManagerServicePath}</controlURL>
				<SCPDURL>/SCPD/connectionManager.xml</SCPDURL>
			</service>
			<service>
				<serviceType>{Services.ServiceType.X_MS_MediaReceiverRegistrar}</serviceType>
				<serviceId>{Services.ServiceId.X_MS_MediaReceiverRegistrar}</serviceId>
				<eventSubURL>/event/eventAction/X_MS_MediaReceiverRegistrar</eventSubURL>
				<controlURL>{EndpointServices.MediaReceiverRegistrarServicePath}</controlURL>
				<SCPDURL>/SCPD/MediaReceiverRegistrar.xml</SCPDURL>
			</service>
			<service>
				<serviceType>{Services.ServiceType.AVTransport}</serviceType>
				<serviceId>{Services.ServiceId.AVTransport}</serviceId>
				<eventSubURL>/event/eventAction/AVTransport</eventSubURL>
				<controlURL>{EndpointServices.AVTransportServicePath}</controlURL>
				<SCPDURL>/SCPD/avTransport.xml</SCPDURL>
			</service>
			<service>
				<serviceType>{Services.ServiceType.ContentDirectory}</serviceType>
				<serviceId>{Services.ServiceId.ContentDirectory}</serviceId>
				<eventSubURL>/event/eventAction/ContentDirectory</eventSubURL>
				<controlURL>{EndpointServices.ContentDirectoryServicePath}</controlURL>
				<SCPDURL>/SCPD/contentDirectory.xml</SCPDURL>
			</service>
		</serviceList>
	</device>
</root>
";
            XmlDocument document = new();
            document.LoadXml(xmlContent);
            var returnString = document.OuterXml + Environment.NewLine;

            return Content(returnString, "text/xml; charset=\"utf-8\"", contentEncoding: Encoding.UTF8);
        }

    }
}
