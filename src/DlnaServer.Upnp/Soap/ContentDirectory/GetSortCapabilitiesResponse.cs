using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.ContentDirectory
{
    /// <summary>
    /// Reply to <c>GetSortCapabilities</c>.
    /// </summary>
    [MessageContract(WrapperName = "GetSortCapabilitiesResponse")]
    [XmlRoot(ElementName = "GetSortCapabilitiesResponse")]
    public sealed class GetSortCapabilitiesResponse
    {
        /// <summary>
        /// <b>SortCaps</b><br />
        /// Properties that may be sorted on. <c>*</c> claims all of them.
        /// </summary>
        [XmlElement(ElementName = "SortCaps")]
        public string SortCaps { get; set; } = "*";
    }
}
