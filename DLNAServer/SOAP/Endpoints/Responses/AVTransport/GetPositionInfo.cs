using System.ServiceModel;
using System.Xml.Serialization;

namespace DLNAServer.SOAP.Endpoints.Responses.AVTransport
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [MessageContract(WrapperName = "GetPositionInfoResponse")]
    [XmlRoot(ElementName = "GetPositionInfoResponse")]
    public class GetPositionInfo
    {
        [XmlElement(ElementName = "Track")]
        public uint Track { get; set; }
        [XmlElement(ElementName = "TrackDuration")]
        public string TrackDuration { get; set; }
        [XmlElement(ElementName = "RelTime")]
        public string RelTime { get; set; }
        [XmlElement(ElementName = "AbsTime")]
        public string AbsTime { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
