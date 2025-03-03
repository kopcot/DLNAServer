using DLNAServer.SOAP.Constants;
using DLNAServer.SOAP.Endpoints.Responses.AVTransport;
using System.ServiceModel;

namespace DLNAServer.SOAP.Endpoints.Interfaces
{
    [ServiceContract(Namespace = Services.ServiceType.AVTransport)]
    public interface IAVTransportService
    {
        [OperationContract(Name = "SetAVTransportURI")]
        Task<SetAVTransportURI> SetAVTransportURI(int InstanceID, string CurrentURI, string CurrentURIMetaData);
        [OperationContract(Name = "Play")]
        Task<Play> Play(int InstanceID, string Speed);
        [OperationContract(Name = "Pause")]
        Task<Pause> Pause(int InstanceID);
        [OperationContract(Name = "Stop")]
        Task<Stop> Stop(int InstanceID);
        [OperationContract(Name = "Seek")]
        Task<Seek> Seek(int InstanceID, string SeekMode, string Target);
        [OperationContract(Name = "GetTransportInfo")]
        Task<GetTransportInfo> GetTransportInfo(int InstanceID);
        [OperationContract(Name = "GetPositionInfo")]
        Task<GetPositionInfo> GetPositionInfo(int InstanceID);

    }
}
