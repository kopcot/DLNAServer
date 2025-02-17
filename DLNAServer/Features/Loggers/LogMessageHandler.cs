using DLNAServer.Configuration;
using DLNAServer.Features.Loggers.Data;
using DLNAServer.Features.Loggers.Interfaces;
using System.Collections.Concurrent;

namespace DLNAServer.Features.Loggers
{
    public class LogMessageHandler : ILogMessageHandler
    {
        private readonly ServerConfig _serverConfig;
        private static ConcurrentQueue<LogEntry>? _logQueue;

        public LogMessageHandler(ServerConfig serverConfig)
        {
            _logQueue ??= new();
            _serverConfig = serverConfig;
        }

        public void AddLogMessage(LogEntry logEntry)
        {
            _logQueue!.Enqueue(logEntry);
            while (_logQueue.LongCount() > _serverConfig.ServerMaxLogMessagesCount)
            {
                _ = _logQueue.TryDequeue(out _);
            }
        }

        public async Task<IEnumerable<LogEntry>> GetLastMessagesAsync(uint messageCount)
        {
            var results = _logQueue!.Reverse().Take((int)messageCount).ToArray();
            return await Task.FromResult(results);
        }
    }
}
