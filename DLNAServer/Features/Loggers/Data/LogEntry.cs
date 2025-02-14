namespace DLNAServer.Features.Loggers.Data
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public record LogEntry
    {
        public string Source { get; set; }
        public string Message { get; set; }
        public string? ExceptionStackTrace { get; set; }
        public LogLevel LogLevel { get; set; }
        public DateTime TimestampLocal { get; set; }
        public DateTime TimestampUtc { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
