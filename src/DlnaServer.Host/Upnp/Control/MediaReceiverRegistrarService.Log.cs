namespace DlnaServer.Host.Upnp.Control
{
    internal sealed partial class MediaReceiverRegistrarService
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "IsValidated requested for device '{DeviceId}'; validated")]
        private partial void LogValidationRequested(string deviceId);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "RegisterDevice requested; nothing is registered because nothing is refused")]
        private partial void LogRegistrationRequested();
    }
}
