using System.ComponentModel.DataAnnotations;
using System.ServiceModel;
using System.Xml.Serialization;

namespace DLNAServer.SOAP.Endpoints.Responses.AVTransport
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [MessageContract(WrapperName = "GetTransportInfoResponse")]
    [XmlRoot(ElementName = "GetTransportInfoResponse")]
    public class GetTransportInfo
    {
        [XmlElement(ElementName = "InstanceID")]
        public string InstanceID { get; set; }
        [XmlElement(ElementName = "CurrentTransportState")]
        [AllowedValues(["STOPPED", "PAUSED_PLAYBACK", "PLAYING", "TRANSITIONING"])]
        public string CurrentTransportState { get; set; }
        [XmlElement(ElementName = "CurrentTransportStatus")]
        [AllowedValues(["OK", "ERROR_OCCURRED"])]
        public string CurrentTransportStatus { get; set; }
        [XmlElement(ElementName = "CurrentSpeed")]
        [AllowedValues("1")]
        public string CurrentSpeed { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
