using DLNAServer.Helpers.Logger;
using DLNAServer.SOAP.Endpoints.Interfaces;
using DLNAServer.SOAP.Endpoints.Responses.AVTransport;

namespace DLNAServer.SOAP.Endpoints
{
    public partial class AVTransportService : IAVTransportService
    {
        private readonly ILogger<ContentDirectoryService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AVTransportService(
            ILogger<ContentDirectoryService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }
        public async Task<SetAVTransportURI> SetAVTransportURI(int InstanceID, string CurrentURI, string CurrentURIMetaData)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(SetAVTransportURI),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            // not found operation in real usage
            WarningSetAVTransportURIRequestInfo(nameof(SetAVTransportURI), InstanceID, CurrentURI, CurrentURIMetaData);

            await Task.CompletedTask;
            return new() { InstanceID = InstanceID };
        }
        public async Task<Play> Play(int InstanceID, string Speed)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(Play),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            // not found operation in real usage
            WarningPlayRequestInfo(nameof(Play), InstanceID, Speed);

            await Task.CompletedTask;
            return new() { };
        }

        public async Task<Pause> Pause(int InstanceID)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(Pause),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            // not found operation in real usage
            WarningPauseRequestInfo(nameof(Pause), InstanceID);

            await Task.CompletedTask;
            return new() { };
        }

        public async Task<Stop> Stop(int InstanceID)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(Stop),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            // not found operation in real usage
            WarningStopRequestInfo(nameof(Stop), InstanceID);

            await Task.CompletedTask;
            return new() { };
        }

        public async Task<Seek> Seek(int InstanceID, string SeekMode, string Target)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(Seek),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            // not found operation in real usage
            WarningSeekRequestInfo(nameof(Seek), InstanceID, SeekMode, Target);

            await Task.CompletedTask;
            return new() { };
        }

        public async Task<GetTransportInfo> GetTransportInfo(int InstanceID)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(GetTransportInfo),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            // not found operation in real usage
            WarningGetTransportInfoRequestInfo(nameof(GetTransportInfo), InstanceID);

            await Task.CompletedTask;
            return new() { };
        }

        public async Task<GetPositionInfo> GetPositionInfo(int InstanceID)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(GetPositionInfo),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            // not found operation in real usage
            WarningGetPositionInfoRequestInfo(nameof(GetPositionInfo), InstanceID);

            await Task.CompletedTask;
            return new() { };
        }
    }
}
