namespace DlnaServer.Core.Hosting
{
    /// <summary>
    /// Requests an in-process restart of the web host. Replaces the reference's mutable static flag,
    /// so the restart decision is injectable and testable rather than ambient process state.
    /// </summary>
    public interface IRestartSignal
    {
        /// <summary>
        /// True when a restart has been requested and the host is expected to be rebuilt after it stops.
        /// </summary>
        bool IsRestartRequested { get; }

        void RequestRestart();

        /// <summary>
        /// Clears the request. Called by the host loop once it has begun acting on it.
        /// </summary>
        void Reset();
    }
}
