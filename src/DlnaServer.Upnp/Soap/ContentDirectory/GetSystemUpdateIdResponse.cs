using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.ContentDirectory
{
    /// <summary>
    /// Reply to <c>GetSystemUpdateID</c>.
    /// </summary>
    [MessageContract(WrapperName = "GetSystemUpdateIDResponse")]
    [XmlRoot(ElementName = "GetSystemUpdateIDResponse")]
    public sealed class GetSystemUpdateIdResponse
    {
        /// <summary>
        /// <b>Id</b><br />
        /// Current library revision counter.
        /// </summary>
        [XmlElement(ElementName = "Id")]
        public uint Id { get; set; }
    }
}
