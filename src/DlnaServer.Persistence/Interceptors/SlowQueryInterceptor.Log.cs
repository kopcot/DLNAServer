namespace DlnaServer.Persistence.Interceptors
{
    internal sealed partial class SlowQueryInterceptor
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "Slow database command: {ElapsedMilliseconds:F1} ms (threshold {ThresholdMilliseconds} ms) - {CommandText}")]
        private partial void LogSlowQuery(double elapsedMilliseconds, int thresholdMilliseconds, string commandText);
    }
}
