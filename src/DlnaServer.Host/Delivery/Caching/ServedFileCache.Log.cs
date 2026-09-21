using DlnaServer.Core.Delivery;

namespace DlnaServer.Host.Delivery.Caching
{
    internal sealed partial class ServedFileCache
    {
        // Reports the configured value and the machine alongside the result, because the clamp is the
        // whole point: a 2 GB box silently gets an eighth of itself rather than whatever was configured,
        // and without all three numbers a surprising budget looks like a bug in the setting.
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Served-bytes cache holds up to {BudgetInMegabytes} MB "
                + "(configured {ConfiguredInMegabytes} MB, machine reports {AvailableInMegabytes} MB)")]
        private partial void LogBudget(
            long budgetInMegabytes,
            int configuredInMegabytes,
            long availableInMegabytes);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Cached {SizeInBytes} bytes of '{FilePath}' as {ContentClass}")]
        private partial void LogStored(string filePath, int sizeInBytes, CachedContentClass contentClass);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Could not read '{FilePath}' into the served-bytes cache; it will be served from disc")]
        private partial void LogReadFailed(string filePath, Exception exception);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Information,
            Message = "File caching was switched off; cached bytes released")]
        private partial void LogClearedAfterDisable();

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Information,
            Message = "Served-bytes cache cleared on request; {EntryCount} entr(ies) dropped")]
        private partial void LogCleared(int entryCount);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Debug,
            Message = "Evicted '{FilePath}' because its content was rewritten. The cache is keyed by "
                + "path, so without this the previous bytes were served until their own expiry.")]
        private partial void LogEvicted(string filePath);
    }
}
