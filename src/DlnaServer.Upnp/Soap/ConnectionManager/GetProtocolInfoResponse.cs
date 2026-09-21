using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.ConnectionManager
{
    /// <summary>
    /// Reply to <c>GetProtocolInfo</c>.
    /// </summary>
    /// <remarks>
    /// Declares which transports and formats the device can source and sink. The reference returns both
    /// fields empty, which reads as "serves nothing" to a renderer that pre-filters on this list.
    /// </remarks>
    [MessageContract(WrapperName = "GetProtocolInfoResponse")]
    [XmlRoot(ElementName = "GetProtocolInfoResponse")]
    public sealed class GetProtocolInfoResponse
    {
        /// <summary>
        /// <b>Source</b><br />
        /// Comma-separated <c>protocolInfo</c> values this server can serve, built from the configured
        /// media extensions.
        /// </summary>
        [XmlElement(ElementName = "Source")]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// <b>Sink</b><br />
        /// Always empty. A MediaServer sources content; it never receives any.
        /// </summary>
        [XmlElement(ElementName = "Sink")]
        public string Sink { get; set; } = string.Empty;
    }
}
