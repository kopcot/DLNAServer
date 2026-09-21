namespace DlnaServer.Core.Hosting
{
    /// <inheritdoc cref="ILibraryIndexLock"/>
    /// <remarks>
    /// One permit, held for the whole operation, released in a <c>finally</c>.
    /// <para>
    /// <b>This lock adds no cancellation protection of its own, and the remark here used to claim it
    /// did.</b> The caller's token is honoured while waiting for the permit and is then handed straight
    /// to the operation, which decides for itself - and <c>LibraryIndexer</c> calls
    /// <c>ThrowIfCancellationRequested</c> inside every paging and batching loop, so a shutdown does
    /// produce a half-finished pass. That is the right trade rather than an oversight: withholding the
    /// token would make a shutdown wait out a pass over 25,000 files. The index is derived data and the
    /// next pass resumes, so a half-finished one costs time, not correctness.
    /// </para>
    /// </remarks>
    public sealed class LibraryIndexLock : ILibraryIndexLock, IDisposable
    {
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
