using System.Globalization;
using System.Xml;
using DlnaServer.Upnp.Constants;

namespace DlnaServer.Upnp.Description
{
    /// <summary>
    /// Builds <c>description.xml</c>, the document a renderer fetches first to learn what this device is.
    /// </summary>
    /// <remarks>
    /// The element set, ordering and vendor extensions are reproduced from the reference, including the
    /// parts that look redundant:
    /// <list type="bullet">
    /// <item><c>dlna:X_DLNADOC</c> appears twice, as <c>DMS-1.50</c> and <c>M-DMS-1.50</c>, which satisfies
    /// both strict and lenient certification checks across TV firmware families.</item>
    /// <item>The <c>sec:</c> namespace here is <c>http://www.sec.co.kr/dlna</c> - note the <c>/dlna</c>
    /// suffix, which the DIDL-Lite documents do not use. Both are reproduced as-is.</item>
    /// <item>The result is passed through <see cref="XmlDocument"/> before being returned. That keeps the
    /// XML declaration but collapses the template's indentation, so the wire form is compact. Reproducing
    /// it exactly is why the round-trip is kept rather than returning the template directly.</item>
    /// </list>
    /// </remarks>
    public static class DeviceDescriptionBuilder
    {
        /// <summary>
        /// Builds the description document for one device identity.
        /// </summary>
        public static string Build(DeviceDescription description)
        {
            ArgumentNullException.ThrowIfNull(description);

            var template = string.Create(
                CultureInfo.InvariantCulture,
                $"""
                <?xml version="1.0" encoding="utf-8"?>
                <root xmlns="urn:schemas-upnp-org:device-1-0"  xmlns:dlna="urn:schemas-dlna-org:device-1-0" xmlns:sec="http://www.sec.co.kr/dlna">
                	<specVersion>
                		<major>1</major>
                		<minor>0</minor>
                	</specVersion>
                	<device>
                		<dlna:X_DLNACAP/>
                		<dlna:X_DLNADOC>DMS-1.50</dlna:X_DLNADOC>
                		<dlna:X_DLNADOC>M-DMS-1.50</dlna:X_DLNADOC>
                		<deviceType>{UpnpServices.MediaServer}</deviceType>
                		<friendlyName>{Escape(description.FriendlyName)}</friendlyName>
                		<manufacturer>{Escape(description.ManufacturerName)}</manufacturer>
                		<manufacturerURL>{Escape(description.ManufacturerUrl)}</manufacturerURL>
                		<modelName>{Escape(description.ModelName)}</modelName>
                		<UDN>uuid:{description.DeviceId}</UDN>
                		<modelURL/>
                		<modelDescription/>
                		<modelNumber/>
                		<serialNumber/>
                		<sec:ProductCap>smi,DCM10,getMediaInfo.sec,getCaptionInfo.sec</sec:ProductCap>
                		<sec:X_ProductCap>smi,DCM10,getMediaInfo.sec,getCaptionInfo.sec</sec:X_ProductCap>
                		<iconList>
                			<icon><mimetype>image/jpeg</mimetype><width>500</width><height>500</height><depth>24</depth><url>/icon/extraLarge.jpg</url></icon>
                			<icon><mimetype>image/png</mimetype><width>500</width><height>500</height><depth>24</depth><url>/icon/extraLarge.png</url></icon>
                			<icon><mimetype>image/jpeg</mimetype><width>120</width><height>120</height><depth>24</depth><url>/icon/large.jpg</url></icon>
                			<icon><mimetype>image/png</mimetype><width>120</width><height>120</height><depth>24</depth><url>/icon/large.png</url></icon>
                			<icon><mimetype>image/jpeg</mimetype><width>48</width><height>48</height><depth>24</depth><url>/icon/small.jpg</url></icon>
                			<icon><mimetype>image/png</mimetype><width>48</width><height>48</height><depth>24</depth><url>/icon/small.png</url></icon>
                		</iconList>
                		<serviceList>
                			<service>
                				<serviceType>{UpnpServices.ServiceType.ConnectionManager}</serviceType>
                				<serviceId>{UpnpServices.ServiceId.ConnectionManager}</serviceId>
                				<eventSubURL>/event/eventAction/ConnectionManager</eventSubURL>
                				<controlURL>{UpnpServices.ControlPath.ConnectionManager}</controlURL>
                				<SCPDURL>/SCPD/connectionManager.xml</SCPDURL>
                			</service>
                			<service>
                				<serviceType>{UpnpServices.ServiceType.MediaReceiverRegistrar}</serviceType>
                				<serviceId>{UpnpServices.ServiceId.MediaReceiverRegistrar}</serviceId>
                				<eventSubURL>/event/eventAction/X_MS_MediaReceiverRegistrar</eventSubURL>
                				<controlURL>{UpnpServices.ControlPath.MediaReceiverRegistrar}</controlURL>
                				<SCPDURL>/SCPD/MediaReceiverRegistrar.xml</SCPDURL>
                			</service>
                			<service>
                				<serviceType>{UpnpServices.ServiceType.AvTransport}</serviceType>
                				<serviceId>{UpnpServices.ServiceId.AvTransport}</serviceId>
                				<eventSubURL>/event/eventAction/AVTransport</eventSubURL>
                				<controlURL>{UpnpServices.ControlPath.AvTransport}</controlURL>
                				<SCPDURL>/SCPD/avTransport.xml</SCPDURL>
                			</service>
                			<service>
                				<serviceType>{UpnpServices.ServiceType.ContentDirectory}</serviceType>
                				<serviceId>{UpnpServices.ServiceId.ContentDirectory}</serviceId>
                				<eventSubURL>/event/eventAction/ContentDirectory</eventSubURL>
                				<controlURL>{UpnpServices.ControlPath.ContentDirectory}</controlURL>
                				<SCPDURL>/SCPD/contentDirectory.xml</SCPDURL>
                			</service>
                		</serviceList>
                	</device>
                </root>
                """);

            // The reference loads and re-serialises before sending. The declaration survives; the
            // indentation does not. These bytes are what devices have been tested against.
            var document = new XmlDocument();
            document.LoadXml(template);

            // A fixed newline, not Environment.NewLine. These bytes are pinned, and that property is "\n"
            // on the Linux server and "\r\n" on a Windows development machine - so the one document whose
            // exact bytes are the contract was the one thing here that differed between the two, and
            // NasBuild.sh's pre-publish test filter does not include this type's fixture.
            return document.OuterXml + "\n";
        }

        /// <remarks>
        /// <see cref="System.Security.SecurityElement.Escape"/> handles the five markup characters and
        /// nothing else. It leaves control characters alone, and most of those are illegal in XML 1.0 at
        /// any escaping - so a <c>FriendlyName</c> carrying one, which <c>config.json</c> can hold
        /// perfectly legally as a JSON escape, made <c>XmlDocument.LoadXml</c> throw below and
        /// <c>description.xml</c> return 500 for the life of the process while SSDP carried on
        /// advertising the server. Stripped rather than escaped, because there is no escaping that makes
        /// them valid and a device name is not the place to preserve a stray U+0001.
        /// </remarks>
        private static string Escape(string value)
        {
            var escaped = System.Security.SecurityElement.Escape(value) ?? string.Empty;

            return escaped.Any(static character => !XmlConvert.IsXmlChar(character))
                ? new string([.. escaped.Where(static character => XmlConvert.IsXmlChar(character))])
                : escaped;
        }
    }
}
