namespace DlnaServer.Core.Hosting
{
    /// <summary>
    /// Asks the next startup to discard the database file and build a fresh one.
    /// </summary>
    /// <remarks>
    /// A signal rather than a delete, because <b>the safe moment to delete a database file is when
    /// nothing is holding it open</b>. Deleting it from a request handler leaves every other scope's
    /// <c>DbContext</c>, the pooled SQLite connections and any in-flight renderer query pointing at a
    /// file that has just gone; SQLite then quietly creates an empty replacement with no schema in it.
    /// Requesting the reset and restarting means the old host is fully disposed before the file is
    /// touched, which is what makes the operation boring.
    /// <para>
    /// Paired with <see cref="IRestartSignal"/> and, like it, held by the host loop so it outlives the
    /// container it was raised in - the request is made inside a container that is about to die.
    /// </para>
    /// </remarks>
    public interface IDatabaseResetSignal
    {
        /// <summary>
        /// True when the database is to be rebuilt from scratch before it is next opened.
        /// </summary>
        bool IsResetRequested { get; }

        void RequestReset();

        /// <summary>
        /// Clears the request. Called by the initializer once it has acted on it, so a reset happens
        /// exactly once rather than on every subsequent restart.
        /// </summary>
        void Reset();
    }
}
