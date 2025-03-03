using DLNAServer.SOAP.Endpoints.Interfaces;

namespace DLNAServer.SOAP.Endpoints
{
    public class MediaReceiverRegistrarService : IMediaReceiverRegistrarService
    {
        private readonly ILogger<MediaReceiverRegistrarService> _logger;
        public MediaReceiverRegistrarService(
            ILogger<MediaReceiverRegistrarService> logger)
        {
            _logger = logger;
        }
    }
}
