using DLNAServer.Types.DLNA;
using System.ServiceModel;

namespace DLNAServer.SOAP.Endpoints.Interfaces
{
    [ServiceContract(Namespace = XmlNamespaces.NS_ServiceType_X_MS_MediaReceiverRegistrar)]
    public interface IMediaReceiverRegistrarService
    {
    }
}
