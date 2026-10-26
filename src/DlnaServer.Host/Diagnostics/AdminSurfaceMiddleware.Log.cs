using System.Net;

namespace DlnaServer.Host.Diagnostics
{
    internal sealed partial class AdminSurfaceMiddleware
    {
        /// <remarks>
        /// Debug, like the SSDP listener's refusal: anything on the internet can send these at any rate,
        /// so at Information an address scan would fill the log. Visible once DebugMode is on.
        /// </remarks>
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Refused a request on port {LocalPort} from {RemoteAddress}: it is not on the local network")]
        private partial void LogRefusedFromOffTheLocalNetwork(IPAddress? remoteAddress, int localPort);
    }
}
