using Microsoft.Extensions.Logging;

namespace DlnaServer.Admin.Pages
{
    public partial class SearchFiles
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "File search refused: a number box is not a whole number (size from '{MinSize}', size to '{MaxSize}', width '{MinWidth}')")]
        private static partial void LogSearchRefused(ILogger logger, string? minSize, string? maxSize, string? minWidth);
    }
}
