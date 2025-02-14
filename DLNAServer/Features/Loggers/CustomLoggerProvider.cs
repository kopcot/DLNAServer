using DLNAServer.Features.Loggers.Interfaces;

namespace DLNAServer.Features.Loggers
{
    public class CustomLoggerProvider : ILoggerProvider
    {
        private readonly ILogMessageHandler _logMessageService;

        public CustomLoggerProvider(ILogMessageHandler logMessageService)
        {
            _logMessageService = logMessageService;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new CustomLogger(categoryName, _logMessageService);
        }
        #region Dispose
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~CustomLoggerProvider()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
