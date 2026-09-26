using DlnaServer.Core.Hosting;
using DlnaServer.Persistence;

namespace DlnaServer.Host.Hosting
{
    /// <summary>
    /// Brings the database into a usable state and records which machine started the server.
    /// </summary>
    /// <remarks>
    /// Every other hosted service that touches the database waits on <see cref="IDatabaseReadySignal"/>
    /// before its first pass. Registration order does not sequence anything - see that interface for why
    /// the "runs first" this remark used to claim was never true. A missing or damaged database is rebuilt
    /// from scratch - the index is derived data that a rescan restores. A database that is merely from a
    /// different machine is reported and left alone; the reference wiped that too.
    /// <para>
    /// A <see cref="BackgroundService"/> rather than a bare <see cref="IHostedService"/>, and that is not
    /// cosmetic: <c>BackgroundServiceExceptionBehavior</c> only governs the former, so while this
    /// implemented the interface directly a failed <c>File.Move</c> during corruption recovery escaped
    /// straight to <c>Main</c>, where the restart loop has no handler. It also brings the name into line
    /// with the six siblings and with README's class-name-suffix contract, which reserves the bare
    /// <c>*Service</c> suffix for the four SOAP endpoints.
    /// </para>
    /// </remarks>
    internal sealed partial class DatabaseInitializerHostedService : BackgroundService
    {
        private const int FailureExitCode = 1;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IDatabaseReadySignal _readySignal;
        private readonly ILogger<DatabaseInitializerHostedService> _logger;

        public DatabaseInitializerHostedService(
            IServiceScopeFactory scopeFactory,
            IHostApplicationLifetime lifetime,
            IDatabaseReadySignal readySignal,
            ILogger<DatabaseInitializerHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _lifetime = lifetime;
            _readySignal = readySignal;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await InitialiseAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // BackgroundServiceExceptionBehavior is Ignore, so without this the host carried on
                // serving against a database that has no schema - every Browse faulting, every admin page
                // erroring, and the reset path leaving the file deleted. A failure here is not something
                // to survive; it is something to report and stop on.
                LogInitialisationFailed(exception);

                // Non-zero, so a supervisor and the operator can tell this from a clean /manage/stop -
                // StopApplication alone ends Main normally and the process exits 0.
                Environment.ExitCode = FailureExitCode;
                _lifetime.StopApplication();
            }
        }

        private async Task InitialiseAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();

            var result = await initializer.InitializeAsync(cancellationToken);

            switch (result.Outcome)
            {
                case DatabaseInitializationOutcome.Created:
                    LogDatabaseCreated();
                    break;

                case DatabaseInitializationOutcome.Reset:
                    LogDatabaseReset();
                    break;

                case DatabaseInitializationOutcome.Recreated:
                    LogDatabaseRecreated(result.CorruptFileBackupPath ?? "(not preserved)");
                    break;

                default:
                    if (result.MigrationsApplied > 0)
                    {
                        LogMigrationsApplied(result.MigrationsApplied);
                    }

                    break;
            }

            var machineName = Environment.MachineName;
            var lastMachineName = await initializer.GetLastMachineNameAsync(cancellationToken);

            if (lastMachineName is not null
                && !string.Equals(lastMachineName, machineName, StringComparison.OrdinalIgnoreCase))
            {
                LogMachineNameChanged(lastMachineName, machineName);
            }

            await initializer.RecordStartupAsync(machineName, cancellationToken);

            // Last, and only on the success path: every service that reads or writes the database is
            // waiting on this, so releasing it early would reinstate the race it exists to remove.
            _readySignal.MarkReady();
            LogDatabaseReady(machineName);
        }

    }
}
