using System.Xml.Schema;
using System.Xml.Serialization;
using DlnaServer.Upnp.Constants;

namespace DlnaServer.Upnp.Didl
{
    /// <summary>
    /// A <c>sec:CaptionInfoEx</c> element: Samsung's way of pointing a television at an item's subtitle.
    /// </summary>
    public sealed class DidlCaptionInfo
    {
        /// <summary>
        /// <b>@sec:type</b><br />
        /// The subtitle format as a bare extension, for example <c>srt</c>.
        /// </summary>
        // Qualified explicitly: XmlSerializer leaves an attribute unprefixed when its namespace is the
        // element's own, and Samsung reads sec:type.
        [XmlAttribute("type", Namespace = XmlNamespaces.Samsung, Form = XmlSchemaForm.Qualified)]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// <b>(element text)</b><br />
        /// The URL the television fetches the subtitle from.
        /// </summary>
        [XmlText]
        public string Url { get; set; } = string.Empty;
    }
}
