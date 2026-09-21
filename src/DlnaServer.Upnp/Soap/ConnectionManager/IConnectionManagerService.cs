using System.ServiceModel;

namespace DlnaServer.Upnp.Soap.ConnectionManager
{
    /// <summary>
    /// The ConnectionManager service: what the device can serve, and over which connections.
    /// </summary>
    /// <remarks>
    /// Action and argument names are fixed by <c>/SCPD/connectionManager.xml</c>, which a renderer fetches
    /// and builds its requests from. Only <c>GetProtocolInfo</c> and <c>GetCurrentConnectionIDs</c> are
    /// mandatory; the other three are optional and exist because the SCPD advertises them.
    /// <para>
    /// Parameter names are PascalCase for the same reason as <see cref="DlnaServer.Upnp.Soap.ContentDirectory.IContentDirectoryService"/>:
    /// SoapCore binds each argument from the SOAP body element of the same name, so a camelCase name
    /// binds nothing and the argument arrives null.
    /// </para>
    /// </remarks>
    [ServiceContract(Namespace = Constants.UpnpServices.ServiceType.ConnectionManager)]
    public interface IConnectionManagerService
    {
        /// <summary>
        /// <b>GetProtocolInfo</b><br />
        /// Lists the transports and formats this device can source and sink.
        /// </summary>
        [OperationContract(Name = "GetProtocolInfo")]
        GetProtocolInfoResponse GetProtocolInfo();

        /// <summary>
        /// <b>GetCurrentConnectionIDs</b><br />
        /// Identifiers of the connections currently in use.
        /// </summary>
        [OperationContract(Name = "GetCurrentConnectionIDs")]
        GetCurrentConnectionIdsResponse GetCurrentConnectionIDs();

        /// <summary>
        /// <b>GetCurrentConnectionInfo</b><br />
        /// Describes one connection.
        /// </summary>
        [OperationContract(Name = "GetCurrentConnectionInfo")]
        GetCurrentConnectionInfoResponse GetCurrentConnectionInfo(int ConnectionID);

        /// <summary>
        /// <b>PrepareForConnection</b><br />
        /// Optional. Reserves a connection before streaming; not needed for HTTP delivery.
        /// </summary>
        [OperationContract(Name = "PrepareForConnection")]
        PrepareForConnectionResponse PrepareForConnection(
            string RemoteProtocolInfo,
            string PeerConnectionManager,
            int PeerConnectionID,
            string Direction);

        /// <summary>
        /// <b>ConnectionComplete</b><br />
        /// Optional. Releases a connection reserved by <c>PrepareForConnection</c>.
        /// </summary>
        [OperationContract(Name = "ConnectionComplete")]
        ConnectionCompleteResponse ConnectionComplete(int ConnectionID);
    }
}
