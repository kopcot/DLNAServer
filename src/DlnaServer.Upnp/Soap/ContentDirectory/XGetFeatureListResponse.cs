using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.ContentDirectory
{
    /// <summary>
    /// Reply to <c>X_GetFeatureList</c>, a Samsung extension to ContentDirectory.
    /// </summary>
    /// <remarks>
    /// Samsung televisions call this before browsing, to discover container roots by content type. The
    /// value is an escaped XML document rather than structured elements, which is why it is one string.
    /// The reference declares an empty array of <c>Feature</c> elements instead - a shape the extension
    /// does not define.
    /// </remarks>
    [MessageContract(WrapperName = "X_GetFeatureListResponse")]
    [XmlRoot(ElementName = "X_GetFeatureListResponse")]
    public sealed class XGetFeatureListResponse
    {
        /// <summary>
        /// <b>FeatureList</b><br />
        /// Escaped <c>Features</c> document. Empty means "no vendor features", which is what this server
        /// offers - the library is browsed through the standard root container.
        /// </summary>
        [XmlElement(ElementName = "FeatureList")]
        public string FeatureList { get; set; } = string.Empty;
    }
}
