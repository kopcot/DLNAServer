namespace DlnaServer.Core.Hosting
{
    /// <inheritdoc cref="IDatabaseResetSignal"/>
    /// <remarks>
    /// Mirrors <see cref="RestartSignal"/>, including outliving the DI container: the host loop holds the
    /// same instance across host rebuilds, so a request raised in the container that is about to die is
    /// still there when the next one starts and the initializer reads it.
    /// </remarks>
    public sealed class DatabaseResetSignal : IDatabaseResetSignal
    {
        private int _resetRequested;

        public bool IsResetRequested => Volatile.Read(ref _resetRequested) != 0;

        public void RequestReset()
        {
            _ = Interlocked.Exchange(ref _resetRequested, 1);
        }

        public void Reset()
        {
            _ = Interlocked.Exchange(ref _resetRequested, 0);
        }
    }
}
