namespace DLNAServer.Features.Loggers.Data
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public readonly struct LogEntry
    {
        public LogEntry(string source, string message, string? exceptionStackTrace, LogLevel logLevel, DateTime timestampLocal, DateTime timestampUtc)
        {
            Source = source;
            Message = message;
            ExceptionStackTrace = exceptionStackTrace;
            LogLevel = logLevel;
            TimestampLocal = timestampLocal;
            TimestampUtc = timestampUtc;
        }

        public readonly string Source { get; init; }
        public readonly string Message { get; init; }
        public readonly string? ExceptionStackTrace { get; init; }
        public readonly LogLevel LogLevel { get; init; }
        public readonly DateTime TimestampLocal { get; init; }
        public readonly DateTime TimestampUtc { get; init; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
