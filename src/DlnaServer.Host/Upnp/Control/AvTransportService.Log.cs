namespace DlnaServer.Host.Upnp.Control
{
    internal sealed partial class AvTransportService
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "AVTransport {Action} requested for instance {InstanceId}; "
                + "this server has no transport, so the reply reports an idle one")]
        private partial void LogActionRequested(string action, int instanceId);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "AVTransport SetAVTransportURI requested for '{CurrentUri}'; nothing is loaded")]
        private partial void LogTransportUriSet(string currentUri);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Information,
            Message = "AVTransport Seek requested for instance {InstanceId} to {Target} in units of {Unit}")]
        private partial void LogSeekRequested(int instanceId, string unit, string target);

        /// <summary>
        /// The polled queries, separated from the user-driven actions above purely by level.
        /// </summary>
        /// <remarks>
        /// Renderers poll <c>GetTransportInfo</c> and <c>GetPositionInfo</c> roughly once a second while
        /// playing, so a 90-minute film logs these on the order of 10,000 times. At Information that
        /// drowns the log for actions whose reply is a hard-coded idle state.
        /// </remarks>
        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "AVTransport {Action} polled for instance {InstanceId}; "
                + "this server has no transport, so the reply reports an idle one")]
        private partial void LogStateQueried(string action, int instanceId);
    }
}
