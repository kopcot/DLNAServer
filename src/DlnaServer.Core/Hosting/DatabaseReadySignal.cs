namespace DlnaServer.Core.Hosting
{
    /// <inheritdoc cref="IDatabaseReadySignal"/>
    /// <remarks>
    /// A <see cref="TaskCompletionSource"/> rather than the capped semaphore the sibling signals use,
    /// because this one is latching: every waiter has to be released, and every later waiter has to find
    /// it already released. <c>RunContinuationsAsynchronously</c> keeps a woken service off the
    /// initializer's own thread.
    /// </remarks>
    public sealed class DatabaseReadySignal : IDatabaseReadySignal
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsReady => _ready.Task.IsCompletedSuccessfully;

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            return _ready.Task.WaitAsync(cancellationToken);
        }

        public void MarkReady()
        {
            _ = _ready.TrySetResult();
        }
    }
}
