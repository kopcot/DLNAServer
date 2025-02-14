using System.ServiceModel;
using System.Xml.Serialization;

namespace DLNAServer.SOAP.Endpoints.Responses.ConnectionManager
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [MessageContract(WrapperName = "GetCurrentConnectionIDsResponse")]
    [XmlRoot(ElementName = "GetCurrentConnectionIDsResponse")]
    public class GetCurrentConnectionIDs
    {
        [XmlElement(ElementName = "ConnectionID")]
        public string ConnectionID { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
