using System.Xml.Serialization;
using DlnaServer.Upnp.Constants;

namespace DlnaServer.Upnp.Didl
{
    /// <summary>
    /// A playable item in a DIDL-Lite response.
    /// </summary>
    /// <remarks>
    /// Element order is fixed by the <c>Order</c> values and matters: renderers parse positionally more
    /// often than the specification would suggest. The ordering here reproduces the reference.
    /// </remarks>
    [XmlType(TypeName = "item", Namespace = XmlNamespaces.DidlLite)]
    public sealed class DidlItem
    {
        /// <summary>
        /// <b>@id</b><br />
        /// Identifier the renderer sends back to browse or play this item.
        /// </summary>
        [XmlAttribute("id")]
        public string ObjectId { get; set; } = string.Empty;

        /// <summary>
        /// <b>@parentID</b><br />
        /// Container this item belongs to. <c>0</c> is the root.
        /// </summary>
        [XmlAttribute("parentID")]
        public string ParentId { get; set; } = "0";

        /// <summary>
        /// <b>@restricted</b><br />
        /// <c>1</c> means the renderer may not modify the item. Always set for a read-only server.
        /// </summary>
        [XmlAttribute("restricted")]
        public string Restricted { get; set; } = "1";

        /// <summary>
        /// <b>res</b><br />
        /// The media resource itself: the URL the renderer fetches to play the file.
        /// </summary>
        [XmlElement(ElementName = "res", Order = 0)]
        public List<DidlResource> Resources { get; set; } = [];

        /// <summary>
        /// <b>upnp:class</b><br />
        /// Kind of item, for example <c>object.item.videoItem</c>. Decides where a renderer files it.
        /// </summary>
        [XmlElement(ElementName = "class", Namespace = XmlNamespaces.Upnp, Order = 2)]
        public string Class { get; set; } = string.Empty;

        /// <summary>
        /// <b>dc:title</b><br />
        /// Name shown in the renderer's list.
        /// </summary>
        [XmlElement(ElementName = "title", Namespace = XmlNamespaces.DublinCore, Order = 4)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// <b>dc:date</b><br />
        /// Creation date of the underlying file, in ISO-8601 round-trip form.
        /// </summary>
        [XmlElement(ElementName = "date", Namespace = XmlNamespaces.DublinCore, Order = 5)]
        public string? Date { get; set; }

        /// <summary>
        /// <b>upnp:videoCodec</b><br />
        /// Codec the video track is encoded with, for example <c>h264</c>.
        /// </summary>
        [XmlElement(ElementName = "videoCodec", Namespace = XmlNamespaces.Upnp, Order = 9)]
        public string? VideoCodec { get; set; }

        /// <summary>
        /// <b>upnp:audioCodec</b><br />
        /// Codec the audio track is encoded with, for example <c>aac</c>.
        /// </summary>
        [XmlElement(ElementName = "audioCodec", Namespace = XmlNamespaces.Upnp, Order = 10)]
        public string? AudioCodec { get; set; }

        /// <summary>
        /// <b>res</b><br />
        /// Thumbnail resource, emitted after the media resource. Ordered late so a renderer reading the
        /// first <c>res</c> gets the playable one.
        /// </summary>
        [XmlElement(ElementName = "res", Order = 100)]
        public DidlResource? ThumbnailResource { get; set; }

        /// <summary>
        /// <b>upnp:albumArtURI</b><br />
        /// URL of the item's preview image.
        /// </summary>
        [XmlElement(ElementName = "albumArtURI", Namespace = XmlNamespaces.Upnp, Order = 101)]
        public string? AlbumArtUri { get; set; }

        /// <summary>
        /// <b>upnp:icon</b><br />
        /// Same URL as <see cref="AlbumArtUri"/>. Some renderers read one, some the other, so the
        /// reference emits both and this does too.
        /// </summary>
        [XmlElement(ElementName = "icon", Namespace = XmlNamespaces.Upnp, Order = 102)]
        public string? Icon { get; set; }
    }
}
