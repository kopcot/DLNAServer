using System.ServiceModel;
using System.Xml.Serialization;

namespace DLNAServer.SOAP.Endpoints.Responses
{
    [MessageContract(WrapperName = "BrowseResponse")]
    [XmlRoot(ElementName = "BrowseResponse")]
    public abstract class BaseRequest
    {
    }
}
