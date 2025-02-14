using System.ServiceModel;
using System.Xml.Serialization;

namespace DLNAServer.SOAP.Endpoints.Responses.ConnectionManager
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [MessageContract(WrapperName = "GetProtocolInfoResponse")]
    [XmlRoot(ElementName = "GetProtocolInfoResponse")]
    public class GetProtocolInfo
    {
        [XmlElement(ElementName = "Source")]
        public string Source { get; set; }
        [XmlElement(ElementName = "Sink")]
        public string Sink { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
