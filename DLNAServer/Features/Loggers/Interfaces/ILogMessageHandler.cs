using DLNAServer.Features.Loggers.Data;

namespace DLNAServer.Features.Loggers.Interfaces
{
    public interface ILogMessageHandler
    {
        void AddLogMessage(LogEntry logEntry);
        Task<IEnumerable<LogEntry>> GetLastMessagesAsync(uint messageCount);
    }
}
