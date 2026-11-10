using DlnaServer.Core.Dlna;
using Microsoft.Extensions.Logging;

// CA1716 objects to the namespace, which the Razor compiler chose for this component's other half - a
// partial has to match it. The generated half is exempt only because the analyzer skips generated code.
#pragma warning disable CA1716
namespace DlnaServer.Admin.Shared
#pragma warning restore CA1716
{
    public partial class FileTypeEditor
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Compatibility profile '{Profile}' of type {PreviousMime} cleared: type {Mime} does not offer it")]
        private static partial void LogProfileCleared(ILogger logger, string profile, DlnaMime previousMime, DlnaMime mime);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "File {FileId} changed to type {Mime}, profile '{Profile}': stored {Stored}")]
        private static partial void LogMappingSaved(ILogger logger, Guid fileId, DlnaMime mime, string? profile, bool stored);
    }
}
