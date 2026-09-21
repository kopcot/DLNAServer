namespace DlnaServer.Host.Upnp.Control
{
    internal sealed partial class ConnectionManagerService
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "GetProtocolInfo answered with source list '{Source}'")]
        private partial void LogProtocolInfoRequested(string source);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "GetCurrentConnectionInfo requested for connection {ConnectionId}")]
        private partial void LogConnectionInfoRequested(int connectionId);

        /// <summary>
        /// Information rather than Debug: a renderer reaching this action expects connection negotiation,
        /// which this server does not do, so it is worth seeing which device asked.
        /// </summary>
        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Information,
            Message = "PrepareForConnection requested for '{RemoteProtocolInfo}' in direction '{Direction}'; "
                + "this server serves over HTTP and reserves nothing")]
        private partial void LogPrepareForConnectionRequested(string remoteProtocolInfo, string direction);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "ConnectionComplete requested for connection {ConnectionId}")]
        private partial void LogConnectionCompleteRequested(int connectionId);
    }
}
