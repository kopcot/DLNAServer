using DlnaServer.Core.Uploads;

namespace DlnaServer.Host.Uploads
{
    public sealed partial class UploadSecurityLog
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "upload from={RemoteAddress} agent=\"{UserAgent}\" language=\"{AcceptLanguage}\" "
                + "device={Fingerprint} into=\"{Destination}\" file=\"{FileName}\" bytes={SizeInBytes} "
                + "outcome={Outcome} reason=\"{Reason}\"")]
        private partial void LogUpload(
            string remoteAddress,
            string userAgent,
            string acceptLanguage,
            string fingerprint,
            string destination,
            string fileName,
            long sizeInBytes,
            UploadOutcome outcome,
            string reason);
    }
}
