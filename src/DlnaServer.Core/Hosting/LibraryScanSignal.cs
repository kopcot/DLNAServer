namespace DlnaServer.Core.Hosting
{
    /// <inheritdoc cref="ILibraryScanSignal"/>
    /// <remarks>
    /// A semaphore capped at one permit, which is what makes repeated requests collapse rather than
    /// queue: the second <c>Release</c> throws <see cref="SemaphoreFullException"/> and that throw is the
    /// answer, not a failure. Reading <c>CurrentCount</c> first and releasing only when it is zero looks
    /// tidier and is a race - two callers can both read zero.
    /// </remarks>
    public sealed class LibraryScanSignal : ILibraryScanSignal, IDisposable
    {
        private readonly SemaphoreSlim _requested = new(0, 1);

        public void RequestScan()
        {
            try
            {
                _ = _requested.Release();
            }
            catch (SemaphoreFullException)
            {
                // A pass is already pending and one is enough - it has not started yet, so it will see
                // whatever this caller wanted it to see.
            }
        }

        public Task WaitForRequestAsync(CancellationToken cancellationToken = default)
        {
            return _requested.WaitAsync(cancellationToken);
        }

        public Task<bool> WaitForRequestAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _requested.WaitAsync(timeout, cancellationToken);
        }

        public void Dispose()
        {
            _requested.Dispose();
        }
    }
}
