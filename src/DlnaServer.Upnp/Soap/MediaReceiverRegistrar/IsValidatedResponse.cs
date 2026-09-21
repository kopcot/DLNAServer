using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.MediaReceiverRegistrar
{
    /// <summary>
    /// Reply to <c>IsValidated</c> on the Microsoft MediaReceiverRegistrar service.
    /// </summary>
    /// <remarks>
    /// The service exists for Xbox and Windows Media Player clients, which ask whether a device may
    /// receive content before they will browse. The reference advertises the service in
    /// <c>description.xml</c> and its SCPD but implements neither of its two actions, so such a client
    /// receives a SOAP fault where it expects an answer.
    /// </remarks>
    [MessageContract(WrapperName = "IsValidatedResponse")]
    [XmlRoot(ElementName = "IsValidatedResponse")]
    public sealed class IsValidatedResponse
    {
        /// <summary>
        /// <b>Result</b><br />
        /// <c>1</c> - every device is validated. This is a read-only server on a trusted LAN, the same
        /// decision already taken for ContentDirectory's <c>IsAuthorized</c>.
        /// </summary>
        [XmlElement(ElementName = "Result")]
        public int Result { get; set; } = 1;
    }
}
