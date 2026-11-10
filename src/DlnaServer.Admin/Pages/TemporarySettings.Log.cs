using Microsoft.Extensions.Logging;

namespace DlnaServer.Admin.Pages
{
    public partial class TemporarySettings
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Televisions blocked from the admin pages for {Hours} hour(s), until {UntilUtc}")]
        private static partial void LogTelevisionsBlocked(ILogger logger, int hours, DateTimeOffset? untilUtc);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "Televisions unblocked from the admin pages")]
        private static partial void LogTelevisionsUnblocked(ILogger logger);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Block duration {Requested} hour(s) was outside the allowed range and became {Clamped}")]
        private static partial void LogBlockHoursClamped(ILogger logger, int requested, int clamped);
    }
}
