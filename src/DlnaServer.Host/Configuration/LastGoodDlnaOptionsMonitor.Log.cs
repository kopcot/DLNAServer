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

        /// <remarks>
        /// Error for the same reason as <see cref="LogServingLastGood"/>. The binder's own message is
        /// included because it names the setting and the type it expected, which is what the operator
        /// has to correct.
        /// </remarks>
        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Error,
            Message = "config.json holds a value of the wrong type, so the server is still using the last "
                + "settings that were valid. Fix it and save again: {Reason}")]
        private partial void LogServingLastGoodAfterBindingFailure(string reason);
    }
}
