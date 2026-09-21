using DlnaServer.Core.Hosting;
using DlnaServer.Persistence.Interceptors;
using DlnaServer.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DlnaServer.Persistence
{
    /// <summary>
    /// Registers the media index database and its repositories.
    /// </summary>
    public static class PersistenceServiceCollectionExtensions
    {
        /// <summary>
        /// Logger category the slow-query interceptor writes under, so the host can route it to its own
        /// log file.
        /// </summary>
        /// <remarks>
        /// A literal rather than <c>typeof(...).FullName</c> because the interceptor is internal to this
        /// assembly. It must track the type's namespace and name; <c>SlowQueryInterceptorTest</c> fails
        /// if it drifts.
        /// </remarks>
        public const string SlowQueryLogCategory = "DlnaServer.Persistence.Interceptors.SlowQueryInterceptor";

        public static IServiceCollection AddDlnaPersistence(
            this IServiceCollection services,
            string connectionString)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

            _ = services.AddSingleton<SqlitePragmaInterceptor>();
            _ = services.AddSingleton<SlowQueryInterceptor>();

            _ = services.AddDbContextPool<DlnaDbContext>((provider, options) =>
            {
                _ = options
                    .UseSqlite(connectionString)
                    .AddInterceptors(
                        provider.GetRequiredService<SqlitePragmaInterceptor>(),
                        provider.GetRequiredService<SlowQueryInterceptor>())

                    // Connection and command events default to Information, which turns every startup
                    // migration into pages of SQL in the log. Demoted to Debug, as the reference does -
                    // failures still surface because errors are logged at their own level.
                    .ConfigureWarnings(static warnings => warnings.Log(
                        (RelationalEventId.ConnectionCreated, LogLevel.Debug),
                        (RelationalEventId.ConnectionDisposed, LogLevel.Debug),
                        (RelationalEventId.ConnectionOpened, LogLevel.Debug),
                        (RelationalEventId.ConnectionClosed, LogLevel.Debug),
                        (RelationalEventId.CommandExecuting, LogLevel.Debug),
                        (RelationalEventId.CommandExecuted, LogLevel.Debug)));
            },
            // NOT the default of 1024. Pooling reuses contexts instead of building one per scope, which
            // is the saving - but the pool RETAINS what it reuses, so an unbounded one would trade a
            // little allocation churn for up to 1024 live contexts and make the footprint worse than no
            // pooling at all. Sixteen covers the real concurrency here: six background services, a
            // handful of admin circuits and whatever a single television has in flight.
            poolSize: 16);

            _ = services.AddScoped<IMediaFileRepository, MediaFileRepository>();
            _ = services.AddScoped<IMediaDirectoryRepository, MediaDirectoryRepository>();
            _ = services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
            _ = services.AddScoped<IIndexMaintenance, IndexMaintenance>();
            _ = services.AddScoped<IUploadDeviceRepository, UploadDeviceRepository>();

            // TryAdd, so the host's own instance wins. The host builds one in Main and registers it
            // before this, because a reset request has to survive the container it was raised in; this is
            // only the default that keeps the assembly usable on its own, as the tests use it.
            services.TryAddSingleton<IDatabaseResetSignal, DatabaseResetSignal>();

            return services;
        }
    }
}
