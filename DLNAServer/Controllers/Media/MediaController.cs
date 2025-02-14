using DLNAServer.Configuration;
using DLNAServer.SOAP.Endpoints;
using DLNAServer.Types.DLNA;
using DLNAServer.Types.UPNP.Interfaces;
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
        private readonly IUPNPDevices _upnpDevices;
        public MediaController(
            ILogger<MediaController> logger,
            ServerConfig serverConfig,
            IUPNPDevices uPNPDevices)
        {
            _logger = logger;
            _serverConfig = serverConfig;
            _upnpDevices = uPNPDevices;
        }
        [HttpGet("description.xml")]
        public IActionResult GetDescription([FromQuery] string uuid)
        {
            _logger.LogDebug($"{nameof(GetDescription)}, {HttpContext.Connection.RemoteIpAddress}:{HttpContext.Connection.RemotePort}  path: '{ControllerContext.HttpContext.Request.Path.Value}',  method: '{HttpContext.Request.Method}'");

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
		<friendlyName>{_serverConfig.DlnaServerFriendlyName}</friendlyName>
		<manufacturer>{_serverConfig.DlnaServerManufacturerName}</manufacturer>
		<manufacturerURL>{_serverConfig.DlnaServerManufacturerUrl}</manufacturerURL>
		<modelName>{_serverConfig.DlnaServerModelName}</modelName>
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
				<serviceType>{XmlNamespaces.NS_ServiceType_ConnectionManager}</serviceType>
				<serviceId>{XmlNamespaces.NS_ServiceId_ConnectionManager}</serviceId>
				<eventSubURL>/event/eventAction/ConnectionManager</eventSubURL>
				<controlURL>{EndpointServices.ConnectionManagerServicePath}</controlURL>
				<SCPDURL>/SCPD/connectionManager.xml</SCPDURL>
			</service>
			<service>
				<serviceType>{XmlNamespaces.NS_ServiceType_X_MS_MediaReceiverRegistrar}</serviceType>
				<serviceId>{XmlNamespaces.NS_ServiceId_X_MS_MediaReceiverRegistrar}</serviceId>
				<eventSubURL>/event/eventAction/X_MS_MediaReceiverRegistrar</eventSubURL>
				<controlURL>{EndpointServices.MediaReceiverRegistrarServicePath}</controlURL>
				<SCPDURL>/SCPD/MediaReceiverRegistrar.xml</SCPDURL>
			</service>
			<service>
				<serviceType>{XmlNamespaces.NS_ServiceType_AVTransport}</serviceType>
				<serviceId>{XmlNamespaces.NS_ServiceId_AVTransport}</serviceId>
				<eventSubURL>/event/eventAction/AVTransport</eventSubURL>
				<controlURL>{EndpointServices.AVTransportServicePath}</controlURL>
				<SCPDURL>/SCPD/avTransport.xml</SCPDURL>
			</service>
			<service>
				<serviceType>{XmlNamespaces.NS_ServiceType_ContentDirectory}</serviceType>
				<serviceId>{XmlNamespaces.NS_ServiceId_ContentDirectory}</serviceId>
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
