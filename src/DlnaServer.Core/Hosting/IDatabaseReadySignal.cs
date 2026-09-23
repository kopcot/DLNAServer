namespace DlnaServer.Core.Hosting
{
    /// <summary>
    /// Announces that the database has a usable schema, and lets a service wait for it.
    /// </summary>
    /// <remarks>
    /// Registration order does not sequence hosted services. Every one of them is a
    /// <c>BackgroundService</c>, whose <c>StartAsync</c> stores the <c>ExecuteAsync</c> task and returns at
    /// its first <c>await</c> - so the initializer yields inside <c>GetPendingMigrationsAsync</c>, before
    /// any table exists, and the host starts the indexer immediately. On a fresh deployment the first scan
    /// then queried a table that migrations were still creating, the failure was swallowed as one
    /// "scan failed" line, and nothing retried: the library stayed empty until an operator noticed.
    /// <para>
    /// This makes the dependency explicit instead of positional. It also matters for the reset path,
    /// whose whole premise - that deleting a database file is safe because nothing has opened it yet -
    /// rested on the same ordering that did not exist. On Linux, deleting a file another scope holds open
    /// succeeds, and the indexer carries on writing into the unlinked inode.
    /// </para>
    /// </remarks>
    public interface IDatabaseReadySignal
    {
        /// <summary>
        /// Whether the schema is usable, answered without waiting.
        /// </summary>
        /// <remarks>
        /// For a caller that must report the current state rather than block until it changes - the
        /// readiness endpoint is the one that needs this. Everything that can wait uses
        /// <see cref="WaitAsync"/>, which is the safer default.
        /// </remarks>
        bool IsReady { get; }

        /// <summary>
        /// Completes once the schema is usable, and stays completed for every later caller.
        /// </summary>
        Task WaitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks the schema usable. Repeated calls are ignored.
        /// </summary>
        void MarkReady();
    }
}
