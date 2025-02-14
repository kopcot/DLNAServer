using DLNAServer.Features.Loggers.Data;
using DLNAServer.Features.Loggers.Interfaces;

namespace DLNAServer.Features.Loggers
{
    public class CustomLogger : ILogger
    {
        private readonly string _name;
        private readonly ILogMessageHandler _logMessageService;

        public CustomLogger(string name, ILogMessageHandler logMessageService)
        {
            _name = name;
            _logMessageService = logMessageService;
        }

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                var message = formatter(state, exception);
                _logMessageService.AddLogMessage(new LogEntry
                {
                    Source = _name,
                    Message = message,
                    LogLevel = logLevel,
                    TimestampLocal = DateTime.Now,
                    TimestampUtc = DateTime.UtcNow,
                    ExceptionStackTrace = exception?.StackTrace,
                });
            }
        }
    }
}
