using DlnaServer.Core.Hosting;
using DlnaServer.Host.Hosting;
using DlnaServer.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the hosted service around <see cref="IDatabaseInitializer"/>, which had no tests.
    /// </summary>
    /// <remarks>
    /// <c>DatabaseInitializerTest</c> covers the initializer itself thoroughly. The two behaviours the
    /// project documents as requirements live one level up, here, and neither was exercised: the
    /// cross-machine warning - the thing the reference got wrong by wiping the database - and the
    /// fail-stop that keeps <c>BackgroundServiceExceptionBehavior.Ignore</c> from leaving the host
    /// serving against a schema-less database.
    /// </remarks>
    [TestFixture]
    internal sealed class DatabaseInitializerHostedServiceTest
    {
        private static DatabaseInitializerHostedService CreateService(
            FakeDatabaseInitializer initializer,
            RecordingApplicationLifetime lifetime,
            IDatabaseReadySignal readySignal)
        {
            var services = new ServiceCollection();
            _ = services.AddScoped<IDatabaseInitializer>(_ => initializer);

            var provider = services.BuildServiceProvider();

            return new DatabaseInitializerHostedService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                lifetime,
                readySignal,
                NullLogger<DatabaseInitializerHostedService>.Instance);
        }

        [Test]
        public async Task StartAsync_OnSuccess_MarksReadyAndRecordsThisMachine()
        {
            // Arrange
            var initializer = new FakeDatabaseInitializer();
            using var lifetime = new RecordingApplicationLifetime();
            var readySignal = new DatabaseReadySignal();

            using var service = CreateService(initializer, lifetime, readySignal);

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask!;

            // Assert
            readySignal.WaitAsync(CancellationToken.None).IsCompleted.Should().BeTrue(
                "because every service that touches the database is waiting on this, and releasing it is "
                + "the whole point of the success path");
            initializer.RecordedMachineName.Should().Be(Environment.MachineName,
                "because the machine that started the server is what the next start compares against");
            lifetime.WasStopRequested.Should().BeFalse("because nothing went wrong");
        }

        /// <summary>
        /// A database last opened elsewhere is reported and left alone.
        /// </summary>
        /// <remarks>
        /// The reference treated a machine-name mismatch as a reason to drop and recreate. It is not
        /// corruption - moving a NAS drive to another box is a perfectly ordinary thing to do - so the
        /// row survives and startup continues.
        /// </remarks>
        [Test]
        public async Task StartAsync_WhenTheDatabaseCameFromAnotherMachine_StillStartsAndKeepsIt()
        {
            // Arrange
            var initializer = new FakeDatabaseInitializer { LastMachineName = "some-other-nas" };
            using var lifetime = new RecordingApplicationLifetime();
            var readySignal = new DatabaseReadySignal();

            using var service = CreateService(initializer, lifetime, readySignal);

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask!;

            // Assert
            readySignal.WaitAsync(CancellationToken.None).IsCompleted.Should().BeTrue(
                "because a database from another machine is usable, and refusing to start over it would "
                + "be the reference's mistake with a different symptom");
            lifetime.WasStopRequested.Should().BeFalse(
                "because this is a warning, not a failure");
            initializer.RecordedMachineName.Should().Be(Environment.MachineName,
                "because this machine now owns it, so the next start compares against this one");
        }

        /// <summary>
        /// A failed initialisation stops the application rather than being swallowed.
        /// </summary>
        /// <remarks>
        /// <c>BackgroundServiceExceptionBehavior</c> is <c>Ignore</c> for this host, deliberately - one
        /// failing background service must not take the server off the network. That makes this the one
        /// service which has to opt out: without the catch, the host carried on serving against a
        /// database with no schema, every Browse faulting and the reset path leaving the file deleted.
        /// </remarks>
        [Test]
        public async Task StartAsync_WhenInitialisationThrows_StopsTheApplicationAndNeverMarksReady()
        {
            // Arrange
            var initializer = new FakeDatabaseInitializer { Fail = true };
            using var lifetime = new RecordingApplicationLifetime();
            var readySignal = new DatabaseReadySignal();

            using var service = CreateService(initializer, lifetime, readySignal);

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask!;

            // Assert
            lifetime.WasStopRequested.Should().BeTrue(
                "because a host that cannot reach its schema has nothing useful left to do, and Ignore "
                + "would otherwise leave it running and failing every request");
            readySignal.WaitAsync(CancellationToken.None).IsCompleted.Should().BeFalse(
                "because the services waiting on this must not be released onto a database that was "
                + "never initialised");
        }

        /// <summary>
        /// A failed initialisation exits non-zero, so it cannot be mistaken for a requested stop.
        /// </summary>
        [Test]
        public async Task StartAsync_WhenInitialisationThrows_SetsAFailingExitCode()
        {
            // Arrange
            var initializer = new FakeDatabaseInitializer { Fail = true };
            using var lifetime = new RecordingApplicationLifetime();
            var readySignal = new DatabaseReadySignal();

            using var service = CreateService(initializer, lifetime, readySignal);

            try
            {
                // Act
                await service.StartAsync(CancellationToken.None);
                await service.ExecuteTask!;

                // Assert
                Environment.ExitCode.Should().NotBe(0,
                    "because StopApplication alone exits 0, which is exactly what /manage/stop looks like");
            }
            finally
            {
                // Process-wide, and this process is the test host.
                Environment.ExitCode = 0;
            }
        }

        private sealed class FakeDatabaseInitializer : IDatabaseInitializer
        {
            public bool Fail { get; init; }

            public string? LastMachineName { get; init; }

            public string? RecordedMachineName { get; private set; }

            public Task<DatabaseInitializationResult> InitializeAsync(
                CancellationToken cancellationToken = default)
            {
                return Fail
                    ? throw new InvalidOperationException("the migration could not be applied")
                    : Task.FromResult(new DatabaseInitializationResult(
                        DatabaseInitializationOutcome.Opened,
                        MigrationsApplied: 0,
                        CorruptFileBackupPath: null));
            }

            public Task<string?> GetLastMachineNameAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(LastMachineName);
            }

            public Task RecordStartupAsync(string machineName, CancellationToken cancellationToken = default)
            {
                RecordedMachineName = machineName;

                return Task.CompletedTask;
            }
        }
    }
}
