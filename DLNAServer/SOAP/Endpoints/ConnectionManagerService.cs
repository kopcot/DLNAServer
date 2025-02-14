using DLNAServer.SOAP.Endpoints.Interfaces;
using DLNAServer.SOAP.Endpoints.Responses.ConnectionManager;

namespace DLNAServer.SOAP.Endpoints
{
    public class ConnectionManagerService : IConnectionManagerService
    {
        private readonly ILogger<ConnectionManagerService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public ConnectionManagerService(
            ILogger<ConnectionManagerService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;

            _logger.LogWarning($"{DateTime.Now} ConnectionManagerEndpointService - constructor");
        }

        public async Task<ConnectionComplete> ConnectionComplete(int connectionID)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogWarning($"{DateTime.Now} ConnectionComplete(ConnectionID: {connectionID})");

            await Task.CompletedTask;
            return new() { };
        }

        public async Task<GetCurrentConnectionIDs> GetCurrentConnectionIDs()
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogWarning($"{DateTime.Now} GetCurrentConnectionIDs()");

            await Task.CompletedTask;
            return new()
            {
                ConnectionID = "0"
            };
        }

        public async Task<GetCurrentConnectionInfo> GetCurrentConnectionInfo(int connectionID)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogWarning($"{DateTime.Now} GetCurrentConnectionInfo(ConnectionID: {connectionID})");

            await Task.CompletedTask;
            return new()
            {
                AVTransportID = 0,
                Direction = "0",
                RcsID = 0,
                PeerConnectionID = "0",
                PeerConnectionManager = 0,
                ProtocolInfo = "0",
                Status = "0",
            };
        }

        public async Task<GetProtocolInfo> GetProtocolInfo()
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogWarning($"{DateTime.Now} GetProtocolInfo()");

            await Task.CompletedTask;
            return new()
            {
                Sink = "",
                Source = ""
            };
        }

        public async Task<PrepareForConnection> PrepareForConnection(string remoteProtocolInfo, string peerConnectionManager, int peerConnectionID, string direction)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogWarning($"{DateTime.Now} PrepareForConnection(RemoteProtocolInfo: {remoteProtocolInfo}, PeerConnectionManager: {peerConnectionManager}, PeerConnectionID: {peerConnectionID}, Direction: {direction})");

            await Task.CompletedTask;
            return new()
            {
                AVTransportID = "0",
                ConnectionID = "0",
                RcsID = "0"
            };
        }
    }
}
