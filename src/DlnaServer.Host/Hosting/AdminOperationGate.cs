using DlnaServer.Core.Hosting;

namespace DlnaServer.Host.Hosting
{
    /// <inheritdoc cref="IAdminOperationGate"/>
    internal sealed class AdminOperationGate : IAdminOperationGate, IDisposable
    {
        // Scoped, so one per Blazor circuit - which is exactly the lifetime of the DbContext it protects.
        private readonly SemaphoreSlim _gate = new(1, 1);

        public async Task<TResult> RunAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);

            await _gate.WaitAsync(cancellationToken);

            try
            {
                return await operation(cancellationToken);
            }
            finally
            {
                _ = _gate.Release();
            }
        }

        public async Task RunAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);

            await _gate.WaitAsync(cancellationToken);

            try
            {
                await operation(cancellationToken);
            }
            finally
            {
                _ = _gate.Release();
            }
        }

        public void Dispose()
        {
            _gate.Dispose();
        }
    }
}
