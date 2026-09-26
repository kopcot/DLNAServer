using System.Net;

namespace DlnaServer.Host.Upnp.Discovery
{
    internal sealed partial class SsdpListenerHostedService
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Listening for SSDP discovery requests on port {Port}")]
        private partial void LogListening(int port);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "SSDP discovery is unavailable ({Reason}). Renderers can still reach the server "
                + "by address, but will not find it automatically.")]
        private partial void LogListenerUnavailable(string reason);

        [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "SSDP listener stopped ({Reason})")]
        private partial void LogListenerFailed(string reason);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "Answered M-SEARCH for '{SearchTarget}' from {RemoteEndPoint} with {ResponseCount} response(s)")]
        private partial void LogSearchAnswered(string searchTarget, string remoteEndPoint, int responseCount);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Debug,
            Message = "Replying to {RemoteEndPoint} failed ({Reason})")]
        private partial void LogReplyFailed(string remoteEndPoint, string reason);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Error,
            Message = "Handling an M-SEARCH from {RemoteEndPoint} failed; discovery continues")]
        private partial void LogSearchFailed(string remoteEndPoint, Exception exception);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Debug,
            Message = "An SSDP receive failed ({Reason}); attempt {ConsecutiveFailures} - still listening")]
        private partial void LogReceiveFailed(string reason, int consecutiveFailures);


        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Warning,
            Message = "SSDP receive failed repeatedly ({Reason}); backing off {BackoffSeconds}s and "
                + "listening again. This used to end discovery for the life of the process, which a "
                + "network blip was enough to trigger.")]
        private partial void LogListenerBackingOff(string reason, int backoffSeconds);

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Warning,
            Message = "Dropped an M-SEARCH from {RemoteEndPoint}: {MaxConcurrentReplies} replies are "
                + "already waiting out their MX delay. SSDP is best-effort and a renderer retries.")]
        private partial void LogSearchDropped(string remoteEndPoint, int maxConcurrentReplies);

        // Takes the endpoint, not its text, so a flood from outside allocates nothing while Debug is off.
        [LoggerMessage(
            EventId = 10,
            Level = LogLevel.Debug,
            Message = "Ignored an M-SEARCH from {RemoteEndPoint}: it is not on the local network")]
        private partial void LogSearchFromOutsideIgnored(IPEndPoint remoteEndPoint);
    }
}
