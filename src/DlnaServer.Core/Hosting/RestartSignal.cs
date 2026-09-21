namespace DlnaServer.Core.Hosting
{
    /// <inheritdoc cref="IRestartSignal"/>
    /// <remarks>
    /// Deliberately outlives the DI container: the host loop holds the same instance across host rebuilds,
    /// which is what lets a restart request survive the container it was raised in.
    /// </remarks>
    public sealed class RestartSignal : IRestartSignal
    {
        private int _restartRequested;

        public bool IsRestartRequested => Volatile.Read(ref _restartRequested) != 0;

        public void RequestRestart()
        {
            _ = Interlocked.Exchange(ref _restartRequested, 1);
        }

        public void Reset()
        {
            _ = Interlocked.Exchange(ref _restartRequested, 0);
        }
    }
}
