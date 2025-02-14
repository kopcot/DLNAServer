using System.ServiceModel;
using System.Xml.Serialization;

namespace DLNAServer.SOAP.Endpoints.Responses.ConnectionManager
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [MessageContract(WrapperName = "GetCurrentConnectionInfoResponse")]
    [XmlRoot(ElementName = "GetCurrentConnectionInfoResponse")]
    public class GetCurrentConnectionInfo
    {
        [XmlElement(ElementName = "RcsID")]
        public uint RcsID { get; set; }
        [XmlElement(ElementName = "AVTransportID")]
        public uint AVTransportID { get; set; }
        [XmlElement(ElementName = "ProtocolInfo")]
        public string ProtocolInfo { get; set; }
        [XmlElement(ElementName = "PeerConnectionManager")]
        public uint PeerConnectionManager { get; set; }
        [XmlElement(ElementName = "PeerConnectionID")]
        public string PeerConnectionID { get; set; }
        [XmlElement(ElementName = "Direction")]
        public string Direction { get; set; }
        [XmlElement(ElementName = "Status")]
        public string Status { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
