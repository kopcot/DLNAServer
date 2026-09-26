namespace DlnaServer.Host
{
    public static partial class Program
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Starting ZEN DLNA Server {Version} on {Runtime}")]
        private static partial void LogStarting(ILogger logger, string version, string runtime);
    }
}
