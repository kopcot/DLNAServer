namespace DlnaServer.Core.Diagnostics
{
    /// <summary>
    /// Refuses renderer traffic for a period, so maintenance can run without devices reconnecting.
    /// </summary>
    /// <remarks>
    /// The reference blocks by awaiting <c>Task.Delay(hours)</c> inside the request that asked for it, so
    /// the HTTP call hangs for the whole period and the block is lost if that connection drops. Here the
    /// block is state with an expiry: the request that sets it returns immediately, and the block ends on
    /// its own.
    /// <para>
    /// The <c>/manage</c> surface is never blocked. Blocking it would make the block unrecoverable
    /// without restarting the process, which is the opposite of a maintenance aid.
    /// </para>
    /// </remarks>
    public interface IApiBlocker
    {
        /// <summary>
        /// True while renderer traffic should be refused.
        /// </summary>
        bool IsBlocked { get; }

        /// <summary>
        /// When the current block lapses, or null when nothing is blocked.
        /// </summary>
        DateTimeOffset? BlockedUntilUtc { get; }

        /// <summary>
        /// Why the block was raised, for the log and the management endpoint.
        /// </summary>
        string? Reason { get; }

        /// <summary>
        /// Refuses traffic for the given period, replacing any block already in force.
        /// </summary>
        void Block(TimeSpan duration, string reason);

        /// <summary>
        /// Ends the block early.
        /// </summary>
        void Release();
    }
}
