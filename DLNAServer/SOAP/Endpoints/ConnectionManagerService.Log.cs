namespace DLNAServer.SOAP.Endpoints
{
    public partial class ConnectionManagerService
    {
        [LoggerMessage(1, LogLevel.Warning, "{operation}(ConnectionID: {connectionID}")]
        partial void WarningConnectionCompleteRequestInfo(string operation, int connectionID);
        [LoggerMessage(2, LogLevel.Warning, "{operation}(ConnectionID: {connectionID}")]
        partial void WarningGetCurrentConnectionInfoRequestInfo(string operation, int connectionID);
        [LoggerMessage(3, LogLevel.Warning, "{operation}(RemoteProtocolInfo: {remoteProtocolInfo}, PeerConnectionManager: {peerConnectionManager}, PeerConnectionID: {peerConnectionID}, Direction: {direction}")]
        partial void WarningGetCurrentConnectionInfoRequestInfo(string operation, string remoteProtocolInfo, string peerConnectionManager, int peerConnectionID, string direction);
    }
}
