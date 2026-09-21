using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.AvTransport
{
    /// <summary>
    /// Reply to <c>GetTransportInfo</c>.
    /// </summary>
    /// <remarks>
    /// Every value here is one the SCPD's <c>allowedValueList</c> permits, so a renderer that validates
    /// the reply accepts it. The reference returns an empty object, which serialises to a response with
    /// no state elements at all, plus an <c>InstanceID</c> the SCPD never declared.
    /// </remarks>
    [MessageContract(WrapperName = "GetTransportInfoResponse")]
    [XmlRoot(ElementName = "GetTransportInfoResponse")]
    public sealed class GetTransportInfoResponse
    {
        /// <summary>
        /// <b>CurrentTransportState</b><br />
        /// Always <c>STOPPED</c> - nothing is ever played by this device. The SCPD also allows
        /// PLAYING, PAUSED_PLAYBACK and TRANSITIONING.
        /// </summary>
        [XmlElement(ElementName = "CurrentTransportState")]
        public string CurrentTransportState { get; set; } = "STOPPED";

        /// <summary>
        /// <b>CurrentTransportStatus</b><br />
        /// <c>OK</c>: stopped is the correct state, not a failure. The alternative is ERROR_OCCURRED.
        /// </summary>
        [XmlElement(ElementName = "CurrentTransportStatus")]
        public string CurrentTransportStatus { get; set; } = "OK";

        /// <summary>
        /// <b>CurrentSpeed</b><br />
        /// <c>1</c>, the only speed the SCPD allows.
        /// </summary>
        [XmlElement(ElementName = "CurrentSpeed")]
        public string CurrentSpeed { get; set; } = "1";
    }
}
