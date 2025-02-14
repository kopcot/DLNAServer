using System.ServiceModel;
using System.Xml.Serialization;

namespace DLNAServer.SOAP.Endpoints.Responses.ConnectionManager
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [MessageContract(WrapperName = "PrepareForConnectionResponse")]
    [XmlRoot(ElementName = "PrepareForConnectionResponse")]
    public class PrepareForConnection
    {
        [XmlElement(ElementName = "ConnectionID")]
        public string ConnectionID { get; set; }
        [XmlElement(ElementName = "AVTransportID")]
        public string AVTransportID { get; set; }
        [XmlElement(ElementName = "RcsID")]
        public string RcsID { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
