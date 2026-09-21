using DlnaServer.Upnp.Soap.MediaReceiverRegistrar;

namespace DlnaServer.Host.Upnp.Control
{
    /// <summary>
    /// Implements the Microsoft <c>X_MS_MediaReceiverRegistrar</c> service.
    /// </summary>
    /// <remarks>
    /// Every device is validated. The server is read-only and on a trusted LAN, which is the same
    /// decision already taken for ContentDirectory's <c>IsAuthorized</c> - refusing a device here would
    /// mean maintaining an allow-list nobody asked for.
    /// </remarks>
    internal sealed partial class MediaReceiverRegistrarService : IMediaReceiverRegistrarService
    {
        private readonly ILogger<MediaReceiverRegistrarService> _logger;

        public MediaReceiverRegistrarService(ILogger<MediaReceiverRegistrarService> logger)
        {
            _logger = logger;
        }

        public IsValidatedResponse IsValidated(string DeviceID)
        {
            LogValidationRequested(DeviceID);

            return new IsValidatedResponse();
        }

        public RegisterDeviceResponse RegisterDevice(string RegistrationReqMsg)
        {
            LogRegistrationRequested();

            return new RegisterDeviceResponse();
        }
    }
}
