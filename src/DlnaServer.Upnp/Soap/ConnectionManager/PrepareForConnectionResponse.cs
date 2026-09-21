using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.ConnectionManager
{
    /// <summary>
    /// Reply to <c>PrepareForConnection</c>.
    /// </summary>
    /// <remarks>
    /// The action is optional in the specification and meaningless here: a renderer fetches content over
    /// HTTP without negotiating a connection first. The reply names the default connection.
    /// </remarks>
    [MessageContract(WrapperName = "PrepareForConnectionResponse")]
    [XmlRoot(ElementName = "PrepareForConnectionResponse")]
    public sealed class PrepareForConnectionResponse
    {
        /// <summary>
        /// <b>ConnectionID</b><br />
        /// The default connection, <c>0</c>.
        /// </summary>
        [XmlElement(ElementName = "ConnectionID")]
        public int ConnectionID { get; set; }

        /// <summary>
        /// <b>AVTransportID</b><br />
        /// <c>-1</c>: no AVTransport instance is allocated for the connection.
        /// </summary>
        [XmlElement(ElementName = "AVTransportID")]
        public int AVTransportID { get; set; } = -1;

        /// <summary>
        /// <b>RcsID</b><br />
        /// <c>-1</c>: the device has no RenderingControl service.
        /// </summary>
        [XmlElement(ElementName = "RcsID")]
        public int RcsID { get; set; } = -1;
    }
}
