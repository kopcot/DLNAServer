using System.Xml.Serialization;

namespace DlnaServer.Upnp.Didl
{
    /// <summary>
    /// A <c>res</c> element: one fetchable representation of an item.
    /// </summary>
    public sealed class DidlResource
    {
        /// <summary>
        /// <b>@protocolInfo</b><br />
        /// Four colon-separated fields describing how the resource may be fetched and decoded. This is
        /// what a renderer matches against its own capabilities before it will play anything.
        /// </summary>
        [XmlAttribute("protocolInfo")]
        public string ProtocolInfo { get; set; } = string.Empty;

        /// <summary>
        /// <b>@size</b><br />
        /// Size of the resource in bytes, so the renderer can show progress and plan its buffering.
        /// </summary>
        [XmlAttribute("size")]
        public string? Size { get; set; }

        /// <summary>
        /// <b>@duration</b><br />
        /// Playing time as <c>H:mm:ss.fff</c>, with hours unbounded rather than wrapping at 24.
        /// </summary>
        [XmlAttribute("duration")]
        public string? Duration { get; set; }

        /// <summary>
        /// <b>@resolution</b><br />
        /// Pixel dimensions as <c>WIDTHxHEIGHT</c>, used by renderers to pick a display mode.
        /// </summary>
        [XmlAttribute("resolution")]
        public string? Resolution { get; set; }

        /// <summary>
        /// <b>@bitrate</b><br />
        /// Bytes per second. Some renderers use it to decide whether the network can keep up.
        /// </summary>
        [XmlAttribute("bitrate")]
        public string? Bitrate { get; set; }

        /// <summary>
        /// <b>@nrAudioChannels</b><br />
        /// Channel count, for example <c>2</c> for stereo.
        /// </summary>
        [XmlAttribute("nrAudioChannels")]
        public string? AudioChannels { get; set; }

        /// <summary>
        /// <b>@sampleFrequency</b><br />
        /// Audio sample rate in hertz.
        /// </summary>
        [XmlAttribute("sampleFrequency")]
        public string? SampleFrequency { get; set; }

        /// <summary>
        /// <b>res</b> (element text)<br />
        /// The element's text: the absolute URL to fetch.
        /// </summary>
        /// <remarks>
        /// Built against the local address the request arrived on, so a multi-homed server hands each
        /// renderer a URL reachable from the interface it is actually using.
        /// </remarks>
        [XmlText]
        public string Url { get; set; } = string.Empty;
    }
}
