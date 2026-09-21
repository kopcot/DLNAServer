namespace DlnaServer.Host.Controllers
{
    public sealed partial class UploadController
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Upload into '{Destination}' finished: {Written} of {Total} file(s) written")]
        private partial void LogFinished(string destination, int total, int written);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "'{FileName}' could not be written into '{Destination}'")]
        private partial void LogWriteFailed(string fileName, string destination, Exception exception);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "The destination for device {Fingerprint} was not recorded; the files were written")]
        private partial void LogDeviceNotRemembered(string fingerprint, Exception exception);
    }
}
