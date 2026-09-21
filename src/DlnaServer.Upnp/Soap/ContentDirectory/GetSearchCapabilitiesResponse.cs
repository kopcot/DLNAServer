using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.ContentDirectory
{
    /// <summary>
    /// Reply to <c>GetSearchCapabilities</c>.
    /// </summary>
    [MessageContract(WrapperName = "GetSearchCapabilitiesResponse")]
    [XmlRoot(ElementName = "GetSearchCapabilitiesResponse")]
    public sealed class GetSearchCapabilitiesResponse
    {
        /// <summary>
        /// <b>SearchCaps</b><br />
        /// Properties that may be searched. <c>*</c> claims all of them.
        /// </summary>
        [XmlElement(ElementName = "SearchCaps")]
        public string SearchCaps { get; set; } = "*";
    }
}
