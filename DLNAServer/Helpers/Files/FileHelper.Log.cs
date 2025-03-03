namespace DLNAServer.Helpers.Files
{
    public static partial class FileHelper
    {
        private static readonly Action<ILogger, Exception?> _logCheckFileSize =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(1, "CheckFileSize"),
            "Check file size.");
        private static void LogCheckFileSize(
            ILogger logger)
        {
            _logCheckFileSize(logger, null);
        }

        private static readonly Action<ILogger, long, long, long, string, Exception?> _logFileSizeIncorrect =
        LoggerMessage.Define<long, long, long, string>(
            LogLevel.Debug,
            new EventId(2, "CheckFileSize"),
            "File size '{fileSize}' incorrect for caching, max. config size {maxFileSize}, max. possible value {maxPossibleSize}, file path = {filePath}");
        private static void LogFileSizeIncorrect(
            ILogger logger,
            long fileSize,
            long maxFileSize,
            long maxPossibleSize,
            string filePath)
        {
            _logFileSizeIncorrect(logger, fileSize, maxFileSize, maxPossibleSize, filePath, null);
        }

        private static readonly Action<ILogger, string, Exception?> _logFileStartReading =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(3, "CheckFileSize"),
            "File reading from disk started, file path: '{filePath}'");
        private static void LogFileStartReading(
            ILogger logger,
            string filePath)
        {
            _logFileStartReading(logger, filePath, null);
        }

        private static readonly Action<ILogger, string, Exception?> _logFileDoneReading =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(4, "CheckFileSize"),
            "File reading from disk done, file path: '{filePath}'");
        private static void LogFileDoneReading(
            ILogger logger,
            string filePath)
        {
            _logFileDoneReading(logger, filePath, null);
        }
    }
}
