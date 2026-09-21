using System.Xml;
using System.Xml.Serialization;
using DlnaServer.Upnp.Constants;

namespace DlnaServer.Upnp.Didl
{
    /// <summary>
    /// Root of a DIDL-Lite result: the payload a renderer parses to list a folder.
    /// </summary>
    /// <remarks>
    /// The namespace prefixes and their order are reproduced from the reference. Containers are declared
    /// before items so folders sort above files in renderers that preserve document order.
    /// </remarks>
    [XmlRoot(ElementName = "DIDL-Lite", Namespace = XmlNamespaces.DidlLite)]
    public sealed class DidlDocument
    {
        /// <summary>
        /// Namespace declarations emitted on the root element.
        /// </summary>
        [XmlNamespaceDeclarations]
        public XmlSerializerNamespaces Namespaces { get; set; } = new(
        [
            new XmlQualifiedName("dc", XmlNamespaces.DublinCore),
            new XmlQualifiedName("dlna", XmlNamespaces.Dlna),
            new XmlQualifiedName("upnp", XmlNamespaces.Upnp),
            new XmlQualifiedName("sec", XmlNamespaces.Samsung),
            new XmlQualifiedName(string.Empty, XmlNamespaces.DidlLite),
        ]);

        /// <summary>
        /// <b>container</b><br />
        /// Browsable folders, emitted before items.
        /// </summary>
        [XmlElement("container")]
        public DidlContainer[] Containers { get; set; } = [];

        /// <summary>
        /// <b>item</b><br />
        /// Playable files.
        /// </summary>
        [XmlElement("item")]
        public DidlItem[] Items { get; set; } = [];
    }
}
