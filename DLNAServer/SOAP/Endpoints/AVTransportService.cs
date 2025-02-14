using DLNAServer.SOAP.Endpoints.Interfaces;
using DLNAServer.SOAP.Endpoints.Responses.AVTransport;

namespace DLNAServer.SOAP.Endpoints
{
    public class AVTransportService : IAVTransportService
    {
        private readonly ILogger<ContentDirectoryService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AVTransportService(
            ILogger<ContentDirectoryService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;

            _logger.LogWarning($"{DateTime.Now} AVTransportEndpointService - constructor");
        }
        public async Task<SetAVTransportURI> SetAVTransportURI(int InstanceID, string CurrentURI, string CurrentURIMetaData)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogWarning($"{DateTime.Now} SetAVTransportURI(InstanceID: {InstanceID}, AVTransportURI: {CurrentURI}, AVTransportURIMetaData: {CurrentURIMetaData}) - not implemented");

            await Task.CompletedTask;
            return new() { InstanceID = InstanceID };
        }
        public async Task<Play> Play(int InstanceID, string Speed)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogWarning($"{DateTime.Now} Play(InstanceID: {InstanceID}, Speed: {Speed}) - not implemented");

            await Task.CompletedTask;
            return new() { };
        }

        public async Task<Pause> Pause(int InstanceID)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogWarning($"{DateTime.Now} Pause(InstanceID: {InstanceID}) - not implemented");

            await Task.CompletedTask;
            return new() { };
        }

        public async Task<Stop> Stop(int InstanceID)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogWarning($"{DateTime.Now} Stop(InstanceID: {InstanceID}) - not implemented");

            await Task.CompletedTask;
            return new() { };
        }

        public async Task<Seek> Seek(int InstanceID, string SeekMode, string Target)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogWarning($"{DateTime.Now} Seek(InstanceID: {InstanceID}, SeekMode: {SeekMode}, Target: {Target}) - not implemented");

            await Task.CompletedTask;
            return new() { };
        }

        public async Task<GetTransportInfo> GetTransportInfo(int InstanceID)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogWarning($"{DateTime.Now} Seek(InstanceID: {InstanceID}) - not implemented");

            await Task.CompletedTask;
            return new() { };
        }

        public async Task<GetPositionInfo> GetPositionInfo(int InstanceID)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogWarning($"{DateTime.Now} Seek(InstanceID: {InstanceID}) - not implemented");

            await Task.CompletedTask;
            return new() { };
        }
    }
}
