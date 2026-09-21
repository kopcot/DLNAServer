namespace DlnaServer.Core.Hosting
{
    /// <summary>
    /// Serialises database work inside one admin circuit.
    /// </summary>
    /// <remarks>
    /// Blazor Server gives each circuit a single DI scope, so every component on a page shares one
    /// <c>DlnaDbContext</c> - and a <c>DbContext</c> permits exactly one operation at a time. Two
    /// components whose handlers interleave at an <c>await</c> therefore throw
    /// <c>InvalidOperationException: A second operation was started on this context instance</c>, from
    /// inside an event handler, which tears down the whole circuit and leaves the operator on a blank
    /// page with a reconnect banner.
    /// <para>
    /// The reachable case: "Recreate metadata" on a large folder awaits for seconds while its own
    /// <c>_busy</c> flag disables only that component's buttons - the folder links stay live, so a click
    /// re-renders the page and issues a second query on the same context.
    /// </para>
    /// <para>
    /// A gate rather than a context factory, deliberately. <c>IDbContextFactory</c> is the textbook
    /// answer, but <c>DlnaDbContext</c> is <c>internal</c> to the persistence assembly - the admin project
    /// cannot name it, which is the entity/DTO boundary working as intended - so handing the UI a factory
    /// would mean widening that boundary to fix a UI concurrency problem. Serialising instead costs
    /// nothing here: one operator, one circuit, and admin queries are not a hot path.
    /// </para>
    /// </remarks>
    public interface IAdminOperationGate
    {
        /// <summary>
        /// Runs <paramref name="operation"/> with no other gated operation in this circuit in flight.
        /// </summary>
        Task<TResult> RunAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs <paramref name="operation"/> with no other gated operation in this circuit in flight.
        /// </summary>
        Task RunAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default);
    }
}
