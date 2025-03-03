using System.Net;

namespace DLNAServer.SSDP
{
    public partial class SSDPListenerService
    {
        [LoggerMessage(1, LogLevel.Information, "Starting SSDP listeners...")]
        partial void InformationStarting();
        [LoggerMessage(2, LogLevel.Information, "Stopping SSDP listeners...")]
        partial void InformationStopping();
        [LoggerMessage(3, LogLevel.Debug, "Listening for M-SEARCH requests...")]
        partial void DebugStartedListening();
        [LoggerMessage(4, LogLevel.Debug, "Listening for M-SEARCH requests... (Cancellation requested)")]
        partial void DebugListeningCancelationRequest();
        [LoggerMessage(5, LogLevel.Debug, "Sent SSDP response to {remoteAddress}:{remotePort}; {uri}")]
        partial void DebugSendResponse(IPAddress remoteAddress, int remotePort, Uri uri);
        [LoggerMessage(6, LogLevel.Error, "Error in Sending message: '{errorMessage}'. Retry in retryDelay {retryDelayInMins:0.00} min")]
        partial void ErrorStopNotifySend(string errorMessage, double retryDelayInMins);
    }
}
