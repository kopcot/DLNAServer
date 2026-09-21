using System.Data.Common;
using DlnaServer.Core.Hosting;
using Microsoft.AspNetCore.Components;

namespace DlnaServer.Admin.Components
{
    /// <summary>
    /// Base for every admin page that touches the database, holding the circuit-lifetime plumbing all of
    /// them need.
    /// </summary>
    /// <remarks>
    /// Six pages each declared their own <see cref="CancellationTokenSource"/>, the same two-line dispose,
    /// and the same gate-and-swallow-cancellation wrapper - which had already been extracted twice,
    /// privately, under two different names, and had drifted: only one of the two also caught
    /// <see cref="DbException"/>.
    /// <para>
    /// The two run methods below are deliberately separate rather than one with a flag. Giving every page
    /// the <see cref="DbException"/> arm would convert a torn-down circuit that <c>MainLayout</c>'s
    /// <c>ErrorBoundary</c> currently catches into an inline message, which is a behaviour change and not
    /// this one's to make - so each page keeps the catch set it had.
    /// </para>
    /// <para>
    /// Public, and in its own namespace rather than <c>Shared</c>, for two compiler reasons: the Razor
    /// compiler emits every page as a public class and a public class cannot derive from an internal base,
    /// and CA1716 rejects a public type in a namespace named after a reserved keyword. Nothing outside
    /// this project has reason to use it.
    /// </para>
    /// </remarks>
    public abstract class AdminPageBase : ComponentBase, IDisposable
    {
        // Cancelled on dispose, so a query does not run on against the circuit's shared DbContext after
        // the operator has navigated away - which is what turned an ordinary navigation into a torn-down
        // circuit.
        private readonly CancellationTokenSource _cts = new();

        [Inject]
        protected IAdminOperationGate Gate { get; set; } = null!;

        /// <summary>
        /// The page's one-line result message, rendered by <c>Notice</c>.
        /// </summary>
        protected string? Message { get; set; }

        /// <summary>
        /// Cancelled when the page goes away, for a call that does not go through the gate.
        /// </summary>
        protected CancellationToken PageToken => _cts.Token;

        /// <summary>
        /// Runs <paramref name="operation"/> through the gate, ignoring the cancellation that navigating
        /// away causes.
        /// </summary>
        protected async Task RunGatedAsync(Func<CancellationToken, Task> operation)
        {
            try
            {
                await Gate.RunAsync(operation, _cts.Token);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                // The operator navigated away. Nothing to report to a page that is gone.
            }
        }

        /// <summary>
        /// Runs <paramref name="operation"/> through the gate and reports a database failure as
        /// <see cref="Message"/> rather than letting it reach the error boundary.
        /// </summary>
        /// <remarks>
        /// <c>DbException</c> rather than <c>SqliteException</c>: it lives in <c>System.Data.Common</c>, so
        /// no package reference is needed and the admin project keeps knowing nothing about the provider.
        /// A <c>database is locked</c> is very reachable while <c>VACUUM</c> holds the file, and an
        /// operator who is not told cannot know whether the work committed.
        /// </remarks>
        protected async Task RunGuardedAsync(Func<CancellationToken, Task> operation, string failureMessage)
        {
            try
            {
                await Gate.RunAsync(operation, _cts.Token);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                // The operator navigated away. Nothing to report to a page that is gone.
            }
            catch (DbException)
            {
                // The provider's own text names error codes and table names, which this interface is not
                // for. The page's sentence plus what to do about it is the whole message.
                Message = failureMessage + " The server was busy - try again in a moment.";
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);

            GC.SuppressFinalize(this);
        }

        /// <remarks>
        /// Cancelled but <b>not disposed</b>. A gated operation may still be running - nothing here waits
        /// for it - and a first-time <c>CancellationToken.Register</c> after disposal throws
        /// <see cref="ObjectDisposedException"/>, which neither run method catches and which would surface
        /// as an unhandled circuit exception. Skipping the dispose costs nothing: the source's wait handle
        /// is never touched, so there is no unmanaged resource to release, and the whole object is
        /// collected with the page.
        /// </remarks>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
            }
        }
    }
}
