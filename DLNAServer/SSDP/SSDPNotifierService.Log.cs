using System.Net;

namespace DLNAServer.SSDP
{
    public partial class SSDPNotifierService
    {
        [LoggerMessage(1, LogLevel.Information, "Starting SSDP notifiers for {endpoint}...")]
        partial void InformationStarting(IPEndPoint endpoint);
        [LoggerMessage(2, LogLevel.Information, "Stopping SSDP notifiers...")]
        partial void InformationStopping();
        [LoggerMessage(3, LogLevel.Debug, "SSDP stop NOTIFY message sent.")]
        partial void DebugStopNotifySend();
        [LoggerMessage(4, LogLevel.Error, "Error in Sending message: '{errorMessage}'. Retry in retryDelay {retryDelayInMins:0.00} min")]
        partial void ErrorStopNotifySend(string errorMessage, double retryDelayInMins);
    }
}
