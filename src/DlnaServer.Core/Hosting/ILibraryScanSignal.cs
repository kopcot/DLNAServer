namespace DlnaServer.Core.Hosting
{
    /// <summary>
    /// Asks for an indexing pass to run, without waiting for one.
    /// </summary>
    /// <remarks>
    /// The seam that lets the admin UI start a scan at all. Scanning lives in the host, and the admin
    /// project references only <c>DlnaServer.Core</c> and <c>DlnaServer.Persistence</c> - the same reason
    /// <see cref="IRestartSignal"/> and <c>IServedFileCache</c> are here rather than beside their
    /// implementations.
    /// <para>
    /// Fire-and-forget on purpose. A pass over a 25,000-file library takes seconds to minutes, and an
    /// admin click that awaited it would hold the circuit's <c>DbContext</c> for the whole time and time
    /// the request out. The caller gets control back immediately; progress goes to the log.
    /// </para>
    /// </remarks>
    public interface ILibraryScanSignal
    {
        /// <summary>
        /// Asks for a pass. Repeated requests while one is already pending collapse into a single pass.
        /// </summary>
        /// <remarks>
        /// Collapsing matters: a scan serialises against every other scan through a process-wide gate, so
        /// queueing one per click would make an impatient operator wait through all of them.
        /// </remarks>
        void RequestScan();

        /// <summary>
        /// Completes once a pass has been asked for, consuming the request.
        /// </summary>
        Task WaitForRequestAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Waits for a request, giving up after <paramref name="timeout"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when a pass was asked for and consumed, <see langword="false"/> when
        /// the wait timed out - which is the caller's cue to run a pass nobody asked for.
        /// </returns>
        /// <remarks>
        /// Exists so <c>Library.UsePeriodicRescan</c> can share this one wait rather than run a timer
        /// beside it. Two waiters would let a timed pass and a requested pass start together, and both
        /// would then serialise on the process-wide index lock for no benefit; here an admin request
        /// simply resets the clock.
        /// </remarks>
        Task<bool> WaitForRequestAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    }
}
