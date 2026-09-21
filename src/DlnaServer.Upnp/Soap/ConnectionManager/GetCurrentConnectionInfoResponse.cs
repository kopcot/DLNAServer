using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.ConnectionManager
{
    /// <summary>
    /// Reply to <c>GetCurrentConnectionInfo</c>.
    /// </summary>
    /// <remarks>
    /// Describes the default connection, which is all this server has: content is fetched over HTTP by
    /// the renderer rather than negotiated. <see cref="Direction"/> and <see cref="Status"/> carry the
    /// values the specification defines; the reference sends the literal string <c>"0"</c> for both.
    /// </remarks>
    [MessageContract(WrapperName = "GetCurrentConnectionInfoResponse")]
    [XmlRoot(ElementName = "GetCurrentConnectionInfoResponse")]
    public sealed class GetCurrentConnectionInfoResponse
    {
        /// <summary>
        /// <b>RcsID</b><br />
        /// RenderingControl instance, or <c>-1</c> when the device has none. This server has none.
        /// </summary>
        [XmlElement(ElementName = "RcsID")]
        public int RcsID { get; set; } = -1;

        /// <summary>
        /// <b>AVTransportID</b><br />
        /// AVTransport instance for the connection, or <c>-1</c> when playback is not server-driven.
        /// </summary>
        [XmlElement(ElementName = "AVTransportID")]
        public int AVTransportID { get; set; } = -1;

        /// <summary>
        /// <b>ProtocolInfo</b><br />
        /// The protocol in use on this connection.
        /// </summary>
        [XmlElement(ElementName = "ProtocolInfo")]
        public string ProtocolInfo { get; set; } = string.Empty;

        /// <summary>
        /// <b>PeerConnectionManager</b><br />
        /// Empty: there is no peer ConnectionManager, because nothing was negotiated.
        /// </summary>
        [XmlElement(ElementName = "PeerConnectionManager")]
        public string PeerConnectionManager { get; set; } = string.Empty;

        /// <summary>
        /// <b>PeerConnectionID</b><br />
        /// <c>-1</c> for the same reason as <see cref="PeerConnectionManager"/>.
        /// </summary>
        [XmlElement(ElementName = "PeerConnectionID")]
        public int PeerConnectionID { get; set; } = -1;

        /// <summary>
        /// <b>Direction</b><br />
        /// <c>Output</c> - this server sends content. The only other legal value is <c>Input</c>.
        /// </summary>
        [XmlElement(ElementName = "Direction")]
        public string Direction { get; set; } = "Output";

        /// <summary>
        /// <b>Status</b><br />
        /// <c>OK</c>. The specification also allows ContentFormatMismatch, InsufficientBandwidth,
        /// UnreliableChannel and Unknown.
        /// </summary>
        [XmlElement(ElementName = "Status")]
        public string Status { get; set; } = "OK";
    }
}
