using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.AvTransport
{
    /// <summary>
    /// Reply to <c>GetPositionInfo</c>.
    /// </summary>
    /// <remarks>
    /// Zeroed, because this device tracks no playback position. The times use the <c>H:mm:ss</c> form the
    /// AVTransport specification requires; an empty string is not a legal duration, and an empty object
    /// is what the reference returns.
    /// </remarks>
    [MessageContract(WrapperName = "GetPositionInfoResponse")]
    [XmlRoot(ElementName = "GetPositionInfoResponse")]
    public sealed class GetPositionInfoResponse
    {
        /// <summary>
        /// <b>Track</b><br />
        /// <c>0</c>: no track is loaded.
        /// </summary>
        [XmlElement(ElementName = "Track")]
        public uint Track { get; set; }

        /// <summary>
        /// <b>TrackDuration</b><br />
        /// <c>0:00:00</c> - unknown duration, expressed as a legal value rather than an empty element.
        /// </summary>
        [XmlElement(ElementName = "TrackDuration")]
        public string TrackDuration { get; set; } = "0:00:00";

        /// <summary>
        /// <b>RelTime</b><br />
        /// Position relative to the start of the track.
        /// </summary>
        [XmlElement(ElementName = "RelTime")]
        public string RelTime { get; set; } = "0:00:00";

        /// <summary>
        /// <b>AbsTime</b><br />
        /// Position relative to the start of the media.
        /// </summary>
        [XmlElement(ElementName = "AbsTime")]
        public string AbsTime { get; set; } = "0:00:00";
    }
}
