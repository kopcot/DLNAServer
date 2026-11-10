using Microsoft.Extensions.Logging;

// CA1716 objects to the namespace, which the Razor compiler chose for this component's other half - a
// partial has to match it. The generated half is exempt only because the analyzer skips generated code.
#pragma warning disable CA1716
namespace DlnaServer.Admin.Shared
#pragma warning restore CA1716
{
    public partial class PageNotFound
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Admin address {Path} answered 404: {Title}")]
        private static partial void LogNotFound(ILogger logger, string? path, string title);
    }
}
