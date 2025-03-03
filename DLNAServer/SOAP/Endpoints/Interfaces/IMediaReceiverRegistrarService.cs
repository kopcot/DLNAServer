using DLNAServer.SOAP.Constants;
using System.ServiceModel;

namespace DLNAServer.SOAP.Endpoints.Interfaces
{
    [ServiceContract(Namespace = Services.ServiceType.X_MS_MediaReceiverRegistrar)]
    public interface IMediaReceiverRegistrarService
    {
    }
}
