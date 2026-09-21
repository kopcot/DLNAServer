namespace DlnaServer.Core.Hosting
{
    /// <summary>
    /// Serialises whole-index work across every scope in the process.
    /// </summary>
    /// <remarks>
    /// Two callers already contended for the index: the startup scan runs for minutes, and the file
    /// watcher starts its own pass whenever a copy settles. Both ask what is already indexed, both are
    /// told a path is new, and both insert it - which fails the unique index on <c>FullPath</c>, and the
    /// resulting <c>DbUpdateException</c> abandons the whole 500-row batch and the rest of that pass. Most
    /// likely on a first deployment, where the startup scan and the first watcher event coincide by
    /// construction. The indexer held a <c>static SemaphoreSlim</c> for that.
    /// <para>
    /// It is an interface now because a <b>third</b> caller needs the same lock and cannot reach a private
    /// static: the admin UI's <i>Rebuild index</i> deletes every row, runs <c>VACUUM</c> and asks for a
    /// scan. Nothing stopped that from running against a scan already in flight - the exact foreign-key
    /// abort the gate was added to prevent, plus <c>VACUUM</c> needing the database to itself. The lock
    /// lives in Core rather than beside the indexer because the two callers are in different assemblies,
    /// and <see cref="IAdminOperationGate"/> is no substitute: that one is <i>scoped</i> and serialises
    /// one circuit's <c>DbContext</c>, while this one is process-wide and serialises the index itself.
    /// </para>
    /// <para>
    /// Registered as a singleton. A caller that cannot get the lock waits; nothing here abandons work
    /// because it is contended, since both an indexing pass and a rebuild have to finish to be correct.
    /// </para>
    /// </remarks>
    public interface ILibraryIndexLock
    {
        /// <summary>
        /// Runs <paramref name="operation"/> with no other whole-index operation in flight.
        /// </summary>
        Task<TResult> RunAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs <paramref name="operation"/> with no other whole-index operation in flight.
        /// </summary>
        Task RunAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default);
    }
}
