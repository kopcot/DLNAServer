using DLNAServer.SOAP.Endpoints.Responses.ConnectionManager;
using DLNAServer.Types.DLNA;
using System.ServiceModel;

namespace DLNAServer.SOAP.Endpoints.Interfaces
{
    [ServiceContract(Namespace = XmlNamespaces.NS_ServiceType_ConnectionManager)]
    public interface IConnectionManagerService
    {
        [OperationContract(Name = "GetProtocolInfo")]
        Task<GetProtocolInfo> GetProtocolInfo();
        [OperationContract(Name = "GetCurrentConnectionIDs")]
        Task<GetCurrentConnectionIDs> GetCurrentConnectionIDs();
        [OperationContract(Name = "PrepareForConnection")]
        Task<PrepareForConnection> PrepareForConnection(string remoteProtocolInfo, string peerConnectionManager, int peerConnectionID, string direction);
        [OperationContract(Name = "GetCurrentConnectionInfo")]
        Task<GetCurrentConnectionInfo> GetCurrentConnectionInfo(int connectionID);
        [OperationContract(Name = "ConnectionComplete")]
        Task<ConnectionComplete> ConnectionComplete(int connectionID);
    }
}
