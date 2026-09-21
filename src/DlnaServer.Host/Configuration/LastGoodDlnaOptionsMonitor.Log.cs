namespace DlnaServer.Host.Configuration
{
    internal sealed partial class LastGoodDlnaOptionsMonitor
    {
        /// <remarks>
        /// Error, not Warning: the running configuration no longer matches the file, which is a state
        /// an operator has to act on rather than merely know about. The failures are included because
        /// they are the same sentences the Settings page shows, and that page cannot be reached while
        /// this is happening.
        /// </remarks>
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Error,
            Message = "config.json did not pass validation, so the server is still using the last "
                + "settings that did. Fix it and save again: {Failures}")]
        private partial void LogServingLastGood(string failures);
    }
}
