using System.Xml.Serialization;
using DlnaServer.Upnp.Constants;

namespace DlnaServer.Upnp.Didl
{
    /// <summary>
    /// A browsable folder in a DIDL-Lite response.
    /// </summary>
    [XmlType(TypeName = "container", Namespace = XmlNamespaces.DidlLite)]
    public sealed class DidlContainer
    {
        /// <summary>
        /// <b>@id</b><br />
        /// Identifier the renderer sends back to browse into this container.
        /// </summary>
        [XmlAttribute("id")]
        public string ObjectId { get; set; } = string.Empty;

        /// <summary>
        /// <b>@parentID</b><br />
        /// Container this one sits in. <c>0</c> is the root.
        /// </summary>
        [XmlAttribute("parentID")]
        public string ParentId { get; set; } = "0";

        /// <summary>
        /// <b>@restricted</b><br />
        /// <c>1</c> means the renderer may not modify the container.
        /// </summary>
        [XmlAttribute("restricted")]
        public string Restricted { get; set; } = "1";

        /// <summary>
        /// <b>@searchable</b><br />
        /// <c>1</c> advertises that the container may be searched.
        /// </summary>
        [XmlAttribute("searchable")]
        public string? Searchable { get; set; } = "1";

        /// <summary>
        /// <b>@childCount</b><br />
        /// Number of children, so a renderer can show a count without browsing into the folder.
        /// </summary>
        [XmlAttribute("childCount")]
        public string? ChildCount { get; set; }

        /// <summary>
        /// <b>upnp:class</b><br />
        /// Container kind, normally <c>object.container</c>.
        /// </summary>
        [XmlElement(ElementName = "class", Namespace = XmlNamespaces.Upnp, Order = 2)]
        public string Class { get; set; } = string.Empty;

        /// <summary>
        /// <b>dc:title</b><br />
        /// Folder name shown in the renderer's list.
        /// </summary>
        [XmlElement(ElementName = "title", Namespace = XmlNamespaces.DublinCore, Order = 4)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// <b>upnp:albumArtURI</b><br />
        /// Folder icon URL.
        /// </summary>
        [XmlElement(ElementName = "albumArtURI", Namespace = XmlNamespaces.Upnp, Order = 101)]
        public string? AlbumArtUri { get; set; }

        /// <summary>
        /// <b>upnp:icon</b><br />
        /// Same URL as <see cref="AlbumArtUri"/>; renderers differ over which they read.
        /// </summary>
        [XmlElement(ElementName = "icon", Namespace = XmlNamespaces.Upnp, Order = 102)]
        public string? Icon { get; set; }
    }
}
