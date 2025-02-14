using System.ServiceModel;
using System.Xml.Serialization;

namespace DLNAServer.SOAP.Endpoints.Responses.AVTransport
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [MessageContract(WrapperName = "SeekResponse")]
    [XmlRoot(ElementName = "SeekResponse")]
    public class Seek
    {
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
