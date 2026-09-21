using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.ContentDirectory
{
    /// <summary>
    /// Reply to <c>IsAuthorized</c>.
    /// </summary>
    [MessageContract(WrapperName = "IsAuthorizedResponse")]
    [XmlRoot(ElementName = "IsAuthorizedResponse")]
    public sealed class IsAuthorizedResponse
    {
        /// <summary>
        /// <b>Result</b><br />
        /// <c>1</c> means authorised. Always granted: this is a read-only server on a trusted LAN.
        /// </summary>
        [XmlElement(ElementName = "Result")]
        public int Result { get; set; } = 1;
    }
}
