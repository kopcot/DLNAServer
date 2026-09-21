using Microsoft.Extensions.Hosting;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// An <see cref="IHostApplicationLifetime"/> that records a shutdown request instead of performing one,
    /// so an endpoint that stops the server can be tested without stopping the test run.
    /// </summary>
    internal sealed class RecordingApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public bool WasStopRequested { get; private set; }

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => _stopped.Token;

        public void StopApplication()
        {
            WasStopRequested = true;
        }

        public void Dispose()
        {
            _started.Dispose();
            _stopping.Dispose();
            _stopped.Dispose();
        }
    }
}
