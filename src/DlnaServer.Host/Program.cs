using System.Reflection;
using System.Runtime.InteropServices;
using DlnaServer.Admin;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Hosting;
using DlnaServer.Host.Configuration;
using DlnaServer.Host.Delivery;
using DlnaServer.Host.Delivery.Caching;
using DlnaServer.Host.Delivery.Prefetch;
using DlnaServer.Host.Diagnostics;
using DlnaServer.Host.Hosting;
using DlnaServer.Host.Indexing;
using DlnaServer.Host.Uploads;
using DlnaServer.Host.Upnp;
using DlnaServer.Media;
using DlnaServer.Persistence;
using DlnaServer.Upnp.Constants;
using DlnaServer.Upnp.Soap;
using DlnaServer.Upnp.Soap.AvTransport;
using DlnaServer.Upnp.Soap.ConnectionManager;
using DlnaServer.Upnp.Soap.ContentDirectory;
using DlnaServer.Upnp.Soap.MediaReceiverRegistrar;
using DlnaServer.Upnp.Ssdp;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Filters;
using SoapCore;
using DlnaServer.Core.Delivery;
using DlnaServer.Core.Diagnostics;
using DlnaServer.Core.Uploads;
using DlnaServer.Core.Subtitles;

namespace DlnaServer.Host
{
    public static partial class Program
    {
        /// <summary>
        /// Name of the configuration file holding <see cref="DlnaOptions"/>, resolved relative to the content root.
        /// </summary>
        private const string ConfigurationFileName = "config.json";

        /// <summary>
        /// Key of the media index connection string in <c>appsettings.json</c>.
        /// </summary>
        private const string DatabaseConnectionName = "DlnaDatabase";

        /// <summary>
        /// The response <c>Content-Security-Policy</c>, applied on both ports.
        /// </summary>
        /// <remarks>
        /// Was <c>frame-ancestors 'none'</c> alone, which is the clickjacking half and nothing else - so
        /// a script source reaching the admin origin had no policy to violate. Each directive here is
        /// what Blazor Server actually needs and no more:
        /// <list type="bullet">
        /// <item><c>default-src 'self'</c> - everything falls back to same-origin.</item>
        /// <item><c>script-src 'self'</c> - <c>blazor.server.js</c> comes from <c>_framework</c>. No
        /// <c>unsafe-eval</c>: that is a Blazor <i>WebAssembly</i> requirement, not a Server one.</item>
        /// <item><c>connect-src 'self' ws: wss:</c> - the circuit is a WebSocket, and a scheme-only
        /// source is needed because <c>'self'</c> does not cover the <c>ws:</c> scheme.</item>
        /// <item><c>img-src 'self' data:</c> - previews are same-origin; <c>data:</c> covers an inline
        /// placeholder.</item>
        /// <item><c>object-src 'none'</c>, <c>base-uri 'self'</c> - nothing needs either.</item>
        /// </list>
        /// <c>style-src</c> allows <c>'unsafe-inline'</c> deliberately: Blazor writes an inline style
        /// attribute for its reconnection overlay, and refusing it leaves an operator staring at a dead
        /// page with no indication why. Verified in a browser, because a wrong directive here breaks the
        /// admin UI silently and no test in this solution can see it.
        /// </remarks>
        private const string ContentSecurityPolicy =
            "default-src 'self'; "
            + "script-src 'self'; "
            + "style-src 'self' 'unsafe-inline'; "
            + "img-src 'self' data:; "
            + "connect-src 'self' ws: wss:; "
            + "object-src 'none'; "
            + "base-uri 'self'; "
            + "frame-ancestors 'none'";

        /// <summary>
        /// Size cap for one <c>app.log</c> / <c>error.log</c> segment, matching <c>slowQuery.log</c>.
        /// </summary>
        private const long AppLogSizeLimitBytes = 32L * 1024 * 1024;

        /// <summary>
        /// Segments kept once the size cap starts rolling within a single day.
        /// </summary>
        /// <remarks>
        /// Serilog applies this and the seven-day time limit together, and its count default of 31 would
        /// keep a gigabyte of one bad afternoon.
        /// <para>
        /// This used to say the count "only bounds a pathological day". That stopped being true on
        /// 2026-09-23, when serving a media file moved to Information: the 32 MB day that produced this
        /// cap was measured with exactly that logging on, so several segments a day is now the ordinary
        /// case and the COUNT can reach back less than seven days on a busy one. Left at 14 rather than
        /// raised, because 14 x 32 MB is already 448 MB in a publish folder on an SMB share - the
        /// trade is a shorter window on heavy days, not more disc.
        /// </para>
        /// </remarks>
        private const int AppLogRetainedFileCountLimit = 14;

        public static async Task Main(string[] args)
        {
            // Anchors config.json, dlna.sqlite and logs/ to the binaries rather than to wherever the
            // process happened to be started from. All three resolved against the working directory:
            // config.json through ContentRootPath, the SQLite Data Source because it is relative, and the
            // Serilog sinks because their paths are. NasBuild.sh cd's to the publish folder first, so the
            // convention held - but only by convention. Started any other way the server built a fresh
            // default config.json in the wrong place, fell back to serving its own folder, created an
            // empty database, and wrote the logs that would have explained it somewhere else again - then
            // came up healthy and advertised itself over SSDP with nothing to serve.
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            // Survives host rebuilds on purpose - a restart request is raised inside a container that is about to die.
            var restartSignal = new RestartSignal();

            // The same trick, for the same reason, with one difference: this one is NOT cleared per
            // iteration. A reset is raised in the container that then asks for the restart, and it has to
            // still be standing when the next container's initializer reads it - which is the only moment
            // the database file can safely be deleted. The initializer clears it once it has acted.
            var databaseResetSignal = new DatabaseResetSignal();

            do
            {
                restartSignal.Reset();

                await using (var app = BuildApplication(args, restartSignal, databaseResetSignal))
                {
                    // Every start, so a log read after a deploy says which build wrote each line.
                    LogStarting(app.Logger, ProductVersion(), RuntimeInformation.FrameworkDescription);

                    await app.RunAsync();
                }
            }
            while (restartSignal.IsRestartRequested);
        }

        // The number the About page shows - the informational version carries the leading zero of MonthDate.
        private static string ProductVersion()
        {
            var assembly = typeof(Program).Assembly;

            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "unknown";
        }

        private static WebApplication BuildApplication(
            string[] args,
            IRestartSignal restartSignal,
            IDatabaseResetSignal databaseResetSignal)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Before the configuration provider sees the file: a malformed config.json would otherwise
            // throw while the host is being built, killing the process with a bare parse error.
            var configurationPath = Path.Combine(builder.Environment.ContentRootPath, ConfigurationFileName);
            var configurationResult = ConfigurationFileGuard.EnsureUsable(configurationPath, TimeProvider.System);

            // The guard above only covers startup, and reloadOnChange makes the same parse failure
            // reachable at runtime: FileConfigurationProvider.Load(reload: true) rethrows on the file
            // watcher's thread-pool callback, where nothing catches it, so the process died. An operator
            // editing config.json over SSH with vi - the documented NAS workflow - could do it, and there
            // is no supervisor to restart the server afterwards. An initial failure still stops the
            // server, because coming up on code defaults means the wrong port and the wrong library.
            // Keeping the last good values across a failed RELOAD is the provider's job, not Ignore's -
            // see LastGoodJsonConfigurationSource for why, and for what it cost when it was not done.
            DlnaConfigurationFile.Add(
                builder.Configuration,
                configurationPath,
                static exception => Serilog.Log.Error(
                    exception,
                    "config.json could not be read on reload. Fix the file - the next restart reads it "
                    + "again, and an unreadable one is replaced with defaults."),
                static reason => Serilog.Log.Warning(
                    "config.json was not re-read because {Reason}, so the server is still running on the "
                    + "values from the last good read. Nothing has changed; fix the file and save it again.",
                    reason));

            // Re-applied last so environment variables still win. AddJsonFile appends its provider after
            // the default environment provider, which would otherwise let the file override an explicit
            // DLNA__... override - the opposite of the usual precedence, and it breaks NAS deployments
            // that configure the server through the environment.
            _ = builder.Configuration.AddEnvironmentVariables();

            // Ports are needed before the host exists, so read them once from the assembled configuration.
            var serverOptions = builder.Configuration
                .GetSection($"{DlnaOptions.SectionName}:{nameof(DlnaOptions.Server)}")
                .Get<ServerOptions>() ?? new ServerOptions();

            // Read once for the same reason the ports are: whether the upload endpoint exists at all is
            // decided while the application is being built, so turning uploads on needs a restart.
            var uploadOptions = builder.Configuration
                .GetSection($"{DlnaOptions.SectionName}:{nameof(DlnaOptions.Upload)}")
                .Get<UploadOptions>() ?? new UploadOptions();

            var startupLogger = ConfigureLogging(builder, serverOptions, out var levelSwitch);
            ConfigureOptions(builder);

            // Reported here, not from a hosted service: options validation runs first and throws on a
            // bad configuration, so a warning raised later would never be seen - precisely when the
            // operator most needs to know their config was replaced.
            ReportConfiguration(startupLogger, configurationResult);
            ReportSourceFolderFallback(startupLogger, builder.Configuration);

            _ = builder.Services.AddSingleton(restartSignal);
            _ = builder.Services.AddSingleton(databaseResetSignal);
            _ = builder.Services.AddSingleton<ILibraryScanSignal, LibraryScanSignal>();
            _ = builder.Services.AddSingleton<ILibraryChangeSignal, LibraryChangeSignal>();
            _ = builder.Services.AddSingleton<IDatabaseReadySignal, DatabaseReadySignal>();

            // Singleton because it serialises the INDEX, not a scope: the startup scan, the file
            // watcher's passes and the admin UI's rebuild all have to queue behind one another.
            _ = builder.Services.AddSingleton<ILibraryIndexLock, LibraryIndexLock>();
            _ = builder.Services.AddSingleton(TimeProvider.System);

            var connectionString = builder.Configuration.GetConnectionString(DatabaseConnectionName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{DatabaseConnectionName}' is missing from configuration.");

            _ = builder.Services.AddDlnaPersistence(connectionString);
            _ = builder.Services.AddDlnaMedia();

            // Replaces the wildcard that appsettings.json ships - see AllowedHostsDefaults for why the
            // wildcard is a real gap. Registration ORDER is not what makes this work, though the comment
            // here used to say so: Options guarantees every Configure delegate runs before any
            // PostConfigure, and the framework binds AllowedHosts with Configure. So this would still see
            // the wildcard registered first. Nothing breaks if it moves.
            _ = builder.Services.AddOptions<HostFilteringOptions>()
                .PostConfigure<ILocalAddressProvider>(static (options, addresses) =>
                    AllowedHostsDefaults.Apply(options, addresses));

            // Delivery: the served-bytes cache and the queue that fills it behind a response. Both are
            // singletons - the cache owns the byte budget for the whole process, and the queue is the
            // one place a request hands work to the background filler.
            _ = builder.Services.AddSingleton<IServedFileCache, ServedFileCache>();
            _ = builder.Services.AddSingleton<IMediaCacheBacklog, MediaCacheBacklog>();

            // One implementation for both delivery paths, so the renderer port and the admin UI cannot
            // drift apart on whether a watched film ends up cached.
            _ = builder.Services.AddSingleton<IMediaContentResolver, MediaContentResolver>();

            _ = builder.Services.AddControllers(options =>
            {
                if (!uploadOptions.Enabled)
                {
                    options.Conventions.Add(new DisabledUploadConvention());
                }
            });

            // The Blazor admin UI. Interactive server components: the UI runs in this process over a
            // SignalR circuit, so a page can read the cache and the repositories directly rather than
            // calling the server's own HTTP surface.
            _ = builder.Services
                .AddRazorComponents()
                .AddInteractiveServerComponents(static options =>
                {
                    // A circuit owns a DI scope, and that scope holds a DlnaDbContext and therefore a
                    // pooled SQLite connection with its own page cache. The framework default retains
                    // 100 disconnected circuits for three minutes, so closing browser tabs could pin a
                    // hundred contexts on a NAS that has one operator. Three covers the real case - a
                    // dropped wifi connection or a reloaded tab - and nothing else.
                    options.DisconnectedCircuitMaxRetained = 3;
                    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(1);

                    // Each buffered batch is a retained render-tree diff. Ten is sized for a lossy
                    // internet connection; this UI is answered over a LAN or not at all.
                    options.MaxBufferedUnacknowledgedRenderBatches = 4;
                });

            _ = builder.Services.AddHttpContextAccessor();

            // Singleton: a block is process-wide state that outlives the request which raised it.
            _ = builder.Services.AddSingleton<IApiBlocker, ApiBlocker>();

            // Singleton for the same reason, and because it caches the composed hidden-folder lists off
            // the configuration change token - a per-request instance would rebuild them per request.
            _ = builder.Services.AddSingleton<ITemporaryFolderVisibility, TemporaryFolderVisibility>();

            // Singleton for the first of those reasons only: the period is process-wide, and every admin
            // page asks on every render.
            _ = builder.Services.AddSingleton<ITechnicalTooltips, TechnicalTooltips>();

            // The report outlives the request that made it by one redirect, so it cannot be scoped; the
            // security log holds nothing per request and is cheaper as one instance.
            _ = builder.Services.AddSingleton<IUploadReportStore, UploadReportStore>();
            _ = builder.Services.AddSingleton<UploadSecurityLog>();

            // The value the endpoint was built from, so the page can tell "off" from "on but not
            // restarted yet" - which the options monitor alone cannot.
            _ = builder.Services.AddSingleton<IUploadAvailability>(
                new UploadAvailability(uploadOptions.Enabled));

            // The settings page writes config.json, and the configuration provider watches that file, so
            // a save is what both persists a change and applies it.
            _ = builder.Services.AddSingleton<ISettingsWriter>(provider => new SettingsWriter(
                configurationPath,
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<SettingsWriter>>()));

            // Lets the settings page report what each source folder actually is before it is saved, rather
            // than the operator finding out from a server that refuses to start.
            _ = builder.Services.AddSingleton<ISourceFolderChecker, SourceFolderChecker>();
            _ = builder.Services.AddSingleton<ISubtitleFileChecker, SubtitleFileChecker>();

            // And lets it ask the question startup asks - defaults applied first, then validated - which
            // it could not do for itself: DlnaOptionsDefaults is internal to this assembly.
            _ = builder.Services.AddSingleton<ISettingsPreflight, SettingsPreflight>();

            // Scoped, so one per Blazor circuit. Serialises the admin UI's database work: every component
            // on a page shares the circuit's single DbContext, and a DbContext allows one operation at a
            // time - two interleaving handlers otherwise throw out of an event handler and kill the circuit.
            // The same class as the index lock, registered separately so the two never share a permit.
            _ = builder.Services.AddScoped<IAdminOperationGate, LibraryIndexLock>();
            _ = builder.Services.AddScoped<ILibraryIndexer, LibraryIndexer>();

            // The admin UI's own formatting and link building. Registered through its project's extension
            // because the services are internal to it, so nothing here can name them.
            _ = builder.Services.AddDlnaAdminServices();

            // A failure in one background service must not take the server off the network. The default
            // is StopHost, and because StopHost calls StopApplication() rather than rethrowing, the
            // restart loop in Main sees no restart request and the process exits 0 - a crashed indexing
            // job would be indistinguishable from /manage/stop. Ignore keeps the host serving and the
            // framework still logs the failure at Error. This is the last of three layers: each service
            // guards per item first and per pass second, so reaching this one means a defect.
            _ = builder.Services.Configure<HostOptions>(static options =>
                options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

            // This order carries no meaning, and saying otherwise cost real time: AddHostedService only
            // decides the sequence StartAsync is CALLED in, and every one of these is a BackgroundService
            // whose StartAsync returns at its first await. The four that touch the database wait on
            // IDatabaseReadySignal instead, which is a dependency the compiler and the runtime both see.
            _ = builder.Services.AddHostedService<DatabaseInitializerHostedService>();
            _ = builder.Services.AddHostedService<LibraryIndexHostedService>();
            _ = builder.Services.AddHostedService<FileWatcherHostedService>();
            _ = builder.Services.AddHostedService<MediaProcessingHostedService>();
            _ = builder.Services.AddHostedService<MediaCacheFillHostedService>();

            // Device identity, the SOAP services, GENA and the two SSDP hosted services. Called here so the
            // hosted services keep their place at the end of the list above.
            _ = builder.Services.AddDlnaUpnp(serverOptions.Port);

            _ = builder.WebHost.ConfigureKestrel(options =>
            {
                // A SOAP Browse body is a few hundred bytes. The 30 MB default let a renderer - or
                // anything on the LAN - post a 30 MB comma-separated Filter, which ParseProperties turns
                // into a HashSet with millions of entries before anything looks at it.
                options.Limits.MaxRequestBodySize = 256 * 1024;

                options.AddServerHeader = false;
                options.ListenAnyIP(serverOptions.Port);
                options.ListenAnyIP(serverOptions.AdminPort);
            });

            var app = builder.Build();

            // Detailed logging follows the file. The switch was captured once from the startup bind and
            // never touched again, so an operator ticking the box on the Settings page saw it saved,
            // reloaded and applied to everything else - and got no more log detail until the next
            // restart, with nothing on the page saying so.
            //
            // Subscribed per host build rather than once per process: the restart loop replaces the
            // container, and the old monitor goes with it, taking this registration along.
            _ = app.Services.GetRequiredService<IOptionsMonitor<DlnaOptions>>().OnChange(options =>
                levelSwitch.MinimumLevel = options.Server.DebugMode
                    ? LogEventLevel.Verbose
                    : LogEventLevel.Information);

            ConfigurePipeline(app, serverOptions);

            return app;
        }

        private static void ConfigureOptions(WebApplicationBuilder builder)
        {
            _ = builder.Services
                .AddOptions<DlnaOptions>()
                .Bind(builder.Configuration.GetSection(DlnaOptions.SectionName))
                .ValidateOnStart();

            // Runs after binding and before validation, so a deployment that omits a source folder gets
            // the fallback rather than a refusal to boot.
            _ = builder.Services.PostConfigure<DlnaOptions>(DlnaOptionsDefaults.Apply);

            _ = builder.Services.AddSingleton<IValidateOptions<DlnaOptions>, DlnaOptionsValidator>();

            // Registered concretely so the container builds and disposes it: it is IDisposable and holds
            // the change-token registrations that make a reload visible.
            _ = builder.Services.AddSingleton<OptionsMonitor<DlnaOptions>>();

            // A registration for the CLOSED type, which the container prefers over the open generic
            // AddOptions installed - so every consumer of IOptionsMonitor<DlnaOptions> gets this one and
            // none of them has to know. It exists because a failed validation is cached and rethrown
            // forever; see LastGoodDlnaOptionsMonitor. ValidateOnStart still resolves through it and
            // still fails the boot, because at that point there is no last good value to fall back to.
            // Registered by its own type as well, and the interface forwards to that one instance rather
            // than building a second: the readiness endpoint needs to ask whether the CURRENT file
            // validates, which is a question IOptionsMonitor<T> has no way to express.
            _ = builder.Services.AddSingleton<LastGoodDlnaOptionsMonitor>(static provider =>
                new LastGoodDlnaOptionsMonitor(
                    provider.GetRequiredService<OptionsMonitor<DlnaOptions>>(),
                    provider.GetRequiredService<ILogger<LastGoodDlnaOptionsMonitor>>()));

            _ = builder.Services.AddSingleton<IOptionsMonitor<DlnaOptions>>(
                static provider => provider.GetRequiredService<LastGoodDlnaOptionsMonitor>());
        }

        private static Serilog.Core.Logger ConfigureLogging(
            WebApplicationBuilder builder,
            ServerOptions serverOptions,
            out LoggingLevelSwitch levelSwitch)
        {
            _ = builder.Logging.ClearProviders();

            // Correlates a log line with the request that produced it, which is what makes TraceId and
            // SpanId usable in the error log below.
            _ = builder.Logging.Configure(static options =>
                options.ActivityTrackingOptions =
                    ActivityTrackingOptions.TraceId
                    | ActivityTrackingOptions.SpanId
                    | ActivityTrackingOptions.ParentId);

            levelSwitch = new LoggingLevelSwitch(
                serverOptions.DebugMode
                    ? LogEventLevel.Verbose
                    : LogEventLevel.Information);

            var configuration = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch);

            // Mirror the per-category levels from appsettings into Serilog. Without this the
            // Logging:LogLevel entries only filter the Microsoft pipeline, and any component writing
            // through Serilog directly ignores them.
            foreach (var (category, level) in ReadCategoryLevels(builder.Configuration))
            {
                _ = configuration.MinimumLevel.Override(category, level);
            }

            var serilog = configuration
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")

                // Slow queries get their own file and are kept out of the application log, so the
                // evidence stays readable: the reference's dedicated log recorded ~106 slow queries in a
                // month and that is what identified the Browse query. Interleaved among every other
                // warning it would be far harder to see.
                .WriteTo.Logger(static sub => sub
                    .Filter.ByIncludingOnly(Matching.FromSource(PersistenceServiceCollectionExtensions.SlowQueryLogCategory))
                    .WriteTo.File(
                        path: Path.Combine("logs", "slowQuery.log"),
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {Message:lj}{NewLine}",
                        rollingInterval: RollingInterval.Month,
                        fileSizeLimitBytes: 32L * 1024 * 1024,

                        // The reference omitted this, so once its monthly file hit the size cap Serilog
                        // silently stopped writing for the rest of the month.
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: 5))
                // Uploads get their own file for a different reason: it is a security record, not a
                // diagnostic one. Someone looking at a file that should not be in the media folders needs
                // to see who sent it, from where and with which browser, without reading it out of
                // everything else the server was doing at the time.
                .WriteTo.Logger(static sub => sub
                    .Filter.ByIncludingOnly(Matching.FromSource(UploadSecurityLog.Category))
                    .WriteTo.File(
                        path: Path.Combine("logs", "uploadSecurity.log"),
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {Message:lj}{NewLine}",
                        rollingInterval: RollingInterval.Month,
                        fileSizeLimitBytes: 32L * 1024 * 1024,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: 12))

                // Both dedicated categories are excluded here, not just the slow-query one: a line that
                // reached app.log as well would be in two files at once, and the newer of the two is the
                // one that would look like the oversight.
                .WriteTo.Logger(static sub => sub
                    .Filter.ByExcluding(Matching.FromSource(PersistenceServiceCollectionExtensions.SlowQueryLogCategory))
                    .Filter.ByExcluding(Matching.FromSource(UploadSecurityLog.Category))
                    .WriteTo.File(
                        path: Path.Combine("logs", "app.log"),
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day,

                        // Capped like slowQuery.log rather than left on Serilog's 1 GB default. A day's
                        // worth of this file reached 32 MB once already, and it lands in the publish
                        // folder on an SMB share, so an unbounded one is both a disc-space and a
                        // disclosure problem.
                        fileSizeLimitBytes: AppLogSizeLimitBytes,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: AppLogRetainedFileCountLimit,
                        retainedFileTimeLimit: TimeSpan.FromDays(7)))
                .WriteTo.File(
                    path: Path.Combine("logs", "error.log"),
                    restrictedToMinimumLevel: LogEventLevel.Error,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{SourceContext}]{NewLine}"
                        + "{Message:lj}{NewLine}TraceId: {TraceId}{NewLine}SpanId: {SpanId}{NewLine}{Exception}{NewLine}",
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: AppLogSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: AppLogRetainedFileCountLimit,
                    retainedFileTimeLimit: TimeSpan.FromDays(7))
                .CreateLogger();

            _ = builder.Logging.AddSerilog(serilog, dispose: true);

            // Also published statically, because the configuration file watcher's OnLoadException fires
            // outside any DI scope and outside the host's lifetime - there is nothing to inject into it.
            // Reassigned on every host rebuild, which is what the restart loop does.
            Serilog.Log.Logger = serilog;

            return serilog;
        }

        /// <summary>
        /// Per-category minimum levels from <c>Logging:LogLevel</c>, excluding the <c>Default</c> entry
        /// which is carried by the level switch.
        /// </summary>
        private static IEnumerable<(string Category, LogEventLevel Level)> ReadCategoryLevels(
            ConfigurationManager configuration)
        {
            var levels = configuration.GetSection("Logging:LogLevel").Get<Dictionary<string, string>>() ?? [];

            foreach (var (category, value) in levels)
            {
                if (string.Equals(category, "Default", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level))
                {
                    yield return (category, level);
                }
            }
        }

        private static void ReportConfiguration(Serilog.Core.Logger logger, ConfigurationFileResult result)
        {
            switch (result.Status)
            {
                // Said out loud rather than inferred from the absence of a warning: an operator reading
                // the log after an edit needs to see that the file was read and taken, and "no complaint"
                // is indistinguishable from the guard never having run.
                case ConfigurationFileStatus.Valid:
                    logger.Information(
                        "Configuration file {FilePath} was read and is well-formed.",
                        result.FilePath);
                    break;

                case ConfigurationFileStatus.Unrecognised:
                    logger.Warning(
                        "Configuration file {FilePath} parses but has no Dlna section, so every setting in "
                        + "it is being ignored and defaults are in use. This server's schema is grouped "
                        + "under Dlna - the reference server's flat file is not compatible. The file has "
                        + "been left exactly as it is.",
                        result.FilePath);
                    break;

                case ConfigurationFileStatus.Created:
                    logger.Information(
                        "No configuration file found. Wrote defaults to {FilePath}. "
                        + "Set Dlna.Library.SourceFolders before the server can serve anything.",
                        result.FilePath);
                    break;

                case ConfigurationFileStatus.Replaced:
                    logger.Warning(
                        "Configuration file {FilePath} was unusable ({Reason}) and has been replaced with "
                        + "defaults. The original was kept at {BackupPath} - recover your settings from it.",
                        result.FilePath,
                        result.Reason,
                        result.BackupPath);
                    break;
            }
        }

        /// <summary>
        /// Says so when no source folder was configured and the application's own folder is being served.
        /// </summary>
        /// <remarks>
        /// Read straight from configuration rather than from the bound options, because the fallback is
        /// applied by <c>PostConfigure</c> and by then the original emptiness is no longer visible. An
        /// operator who forgot the setting should learn it from the log, not from an empty library.
        /// </remarks>
        private static void ReportSourceFolderFallback(Serilog.Core.Logger logger, ConfigurationManager configuration)
        {
            var configured = configuration
                .GetSection($"{DlnaOptions.SectionName}:{nameof(DlnaOptions.Library)}:{nameof(LibraryOptions.SourceFolders)}")
                .Get<string[]>();

            if (configured is not null && configured.Any(static folder => !string.IsNullOrWhiteSpace(folder)))
            {
                return;
            }

            logger.Warning(
                "No source folders are configured, so the application folder {FolderPath} is being served. "
                + "Set Dlna.Library.SourceFolders to serve your media instead.",
                AppContext.BaseDirectory);
        }

        private static void ConfigurePipeline(WebApplication app, ServerOptions serverOptions)
        {
            // Outermost, because it only catches - it never serves or refuses a request, so it does not
            // displace AdminSurfaceMiddleware's position below. Two things it buys: an unhandled
            // exception becomes a plain 500 instead of a torn connection, and the body stays empty. The
            // second matters here - an exception page on a media server hands any device on the LAN the
            // filesystem layout, and the paths in this application's exceptions are the library's.
            _ = app.UseExceptionHandler(new ExceptionHandlerOptions
            {
                AllowStatusCode404Response = true,
                ExceptionHandler = static context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    return Task.CompletedTask;
                },
            });

            // Immediately inside the exception handler, so everything below reads the client's own scheme
            // and address rather than the proxy's. docker-compose.admin-remote.yml puts Caddy in front of
            // the admin port and terminates TLS there, so without this every request behind it looks like
            // plain HTTP from 127.0.0.1 - which would silently reduce the cookie policy below to a no-op
            // in the one deployment that has TLS, and reduce logs/uploadSecurity.log and the
            // UploadDevices table to a column of loopback addresses.
            //
            // KnownProxies and KnownNetworks are deliberately left at their defaults, which trust loopback
            // and nothing else. Caddy runs with network_mode: host, so it reaches the server over
            // 127.0.0.1 and is trusted, while a device on the LAN is not and cannot spoof either header.
            // Clearing those two collections is the usual way this middleware turns into a vulnerability.
            //
            // The socket's own peer is recorded first, because the rewrite below replaces it and the admin
            // port's local-network check has to judge the proxy's connection, not the client it vouches for.
            _ = app.Use((context, next) =>
            {
                AdminSurfaceMiddleware.RecordConnectionPeer(context, serverOptions.AdminPort);

                return next(context);
            });

            _ = app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor,
            });

            // Response headers, before anything can serve. Cheap, and each closes a real gap:
            //
            // frame-ancestors / X-Frame-Options - the admin origin is unauthenticated by design, and its
            // destructive actions are gated only by an arm-then-confirm pair of clicks. That is a
            // usability guard, not a security boundary, and it is exactly what a clickjacking overlay
            // defeats: two decoy clicks in the right places reach Recreate database. Framing was the one
            // route by which a hostile page could reach those handlers, since driving the Blazor circuit
            // cross-site needs a negotiate response it cannot read.
            //
            // nosniff - media is served inline with a MIME type taken from the configured extension map, so a
            // file is never reinterpreted as something more dangerous than its extension says.
            //
            // It does NOT keep script off the admin origin, and this comment used to claim nothing
            // script-capable was served. Since standing decision 16, .svg is indexed from the MIME catalog
            // and served as image/svg+xml, and uploading accepts it: an SVG opened directly is a document
            // that can carry script, so "anyone who can write to the media share" reaches stored XSS on the
            // admin origin. What stops it is the CSP below - script-src 'self' with no 'unsafe-inline' -
            // plus CSP: sandbox on the media controllers' SVG responses. Loosening script-src reopens it.
            //
            // Safe on the renderer port too: a television parses the body, not these headers.
            _ = app.Use(static async (context, next) =>
            {
                var headers = context.Response.Headers;

                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Content-Security-Policy"] = ContentSecurityPolicy;

                // same-origin, NOT no-referrer, and the difference is load-bearing rather than a
                // preference. A navigation-mode POST serializes its Origin header as the literal "null"
                // when the document's referrer policy is no-referrer, so every HTML form on the admin
                // pages posted with Origin: null and RejectCrossSiteEndpointFilter refused all of them
                // with a 403 - the upload page being the only such form, and the only visible casualty.
                // same-origin nulls the Origin only when the request really is cross-origin, which is
                // exactly the signal that filter needs, and nothing leaves this origin either way.
                headers["Referrer-Policy"] = "same-origin";

                await next(context);
            });

            // First of the middleware that can serve or refuse a request, so it sees every one of them,
            // including the SOAP endpoints and anything that later short-circuits. Silent unless
            // Dlna.Server.DebugMode turns Debug logging on.
            // Before the connection log and the endpoints: a blocked request should be refused without
            // reaching a controller, and /manage is exempt inside the middleware so a block stays liftable.
            // Also first so nothing on the media port can reach the admin surface - not a page, not the
            // Blazor circuit, not the class library's stylesheet. Before UseStaticFiles for that reason.
            _ = app.UseMiddleware<AdminSurfaceMiddleware>(serverOptions.AdminPort);

            // Above UseStaticFiles, so a block really does see every request. Below it, static responses
            // still returned 200 while everything else was refused, which contradicted this middleware's
            // own comment about being first in the pipeline.
            _ = app.UseMiddleware<ApiBlockingMiddleware>();

            // Serves the admin stylesheet out of the Razor class library, under
            // _content/DlnaServer.Admin/. Also the Blazor framework files.
            _ = app.UseStaticFiles();
            _ = app.UseMiddleware<ConnectionLoggingMiddleware>();

            // Below the static files, which need no schema and are what the admin page is built from, and
            // above everything that queries. IDatabaseReadySignal sequenced the background services but
            // not the pipeline, so on a fresh deployment - and again right after Recreate database -
            // Kestrel accepted requests while migrations were still running and answered a bare 500.
            _ = app.UseMiddleware<DatabaseReadyMiddleware>();

            // Above everything that can write a cookie, and stated once here rather than on each
            // CookieOptions, so a cookie added by a future page inherits it instead of having to remember.
            //
            // SameAsRequest, NOT Always. The admin UI ships on plain HTTP, and Always would make the
            // browser refuse to store the cookie there: the upload page's remembered destination would
            // quietly stop working, with nothing in any log to say why and no test able to catch it,
            // because it is the browser that drops it. Behind the TLS proxy the same cookie goes out
            // Secure, which is the only place the flag does anything.
            //
            // HttpOnly is forced because this server has no JavaScript and no JS interop at all, so no
            // cookie here is ever meant to be read by a script.
            _ = app.UseCookiePolicy(new CookiePolicyOptions
            {
                Secure = CookieSecurePolicy.SameAsRequest,
                HttpOnly = HttpOnlyPolicy.Always,
            });

            // No UseResponseCaching. It was registered and in the pipeline, and it could never store a
            // single byte: the middleware caches only responses marked Cache-Control public, and all
            // seven [ResponseCache] attributes in this server use ResponseCacheLocation.Client, which
            // emits private. So it ran on every request and held open a 100 MB default store that
            // nothing could ever put anything into. The renderer-side caching those attributes ask for
            // is unaffected - it is the response header doing that work, not this middleware.
            _ = app.UseRouting();

            // Must sit between UseRouting and the endpoints, which is why it is here rather than beside
            // the other middleware above: interactive Razor components carry anti-forgery metadata, and
            // without this in that exact position every admin page fails at request time with a 500
            // while startup reports no problem at all.
            _ = app.UseAntiforgery();

            // Controllers carry the UPnP surface: description.xml, SCPD documents and icons. They are
            // reachable only on the media port; the admin port serves the Blazor UI.
            // Controllers now straddle both ports - AdminMediaController serves the admin UI's previews
            // while every other controller serves renderers - so the group is filtered by path prefix
            // rather than pinned to one port.
            // The second filter is the CSRF defence for /manage. The controller's own remark used to claim
            // the POST verb was the whole defence, which is false: CORS governs whether an attacker may
            // read the response, never whether the browser sends the request, and a cross-origin form
            // POST is a CORS-simple request that arrives and executes. UseAntiforgery below validates
            // only endpoints carrying anti-forgery metadata - Razor components - and no MVC action here
            // has any, so nothing was checking. See RejectCrossSiteEndpointFilter.
            _ = app.MapControllers()
                .AddEndpointFilter(new AdminOrMediaPortEndpointFilter(serverOptions.Port, serverOptions.AdminPort))
                .AddEndpointFilter(new RejectCrossSiteEndpointFilter())
                .AddEndpointFilter(new RejectRemoteManagementEndpointFilter());

            // SOAP control endpoints, one per advertised service. Each path is what description.xml
            // advertises as that service's controlURL, so a renderer reaches it from the description
            // alone. All four are advertised, so all four must answer.
            var soapEncoder = new SoapEncoderOptions();

            _ = ((IApplicationBuilder)app).UseSoapEndpoint<IContentDirectoryService, CustomEnvelopeMessage>(
                path: UpnpServices.ControlPath.ContentDirectory,
                encoder: soapEncoder,
                serializer: SoapSerializer.XmlSerializer);

            _ = ((IApplicationBuilder)app).UseSoapEndpoint<IConnectionManagerService, CustomEnvelopeMessage>(
                path: UpnpServices.ControlPath.ConnectionManager,
                encoder: soapEncoder,
                serializer: SoapSerializer.XmlSerializer);

            _ = ((IApplicationBuilder)app).UseSoapEndpoint<IAvTransportService, CustomEnvelopeMessage>(
                path: UpnpServices.ControlPath.AvTransport,
                encoder: soapEncoder,
                serializer: SoapSerializer.XmlSerializer);

            _ = ((IApplicationBuilder)app).UseSoapEndpoint<IMediaReceiverRegistrarService, CustomEnvelopeMessage>(
                path: UpnpServices.ControlPath.MediaReceiverRegistrar,
                encoder: soapEncoder,
                serializer: SoapSerializer.XmlSerializer);

            // The admin surface is reachable only on its own port, so a renderer on the media port can never see it.
            _ = app.MapGet("/health", static () => Results.Ok("dlna"))
                .AddEndpointFilter(new RequirePortEndpointFilter(serverOptions.Port));

            _ = app.MapGet("/admin/health", static () => Results.Ok("admin"))
                .AddEndpointFilter(new RequirePortEndpointFilter(serverOptions.AdminPort));

            // Readiness, as distinct from the liveness above: the process answering says nothing about
            // whether it can serve anything. Reading CurrentValue is what re-runs validation, so the
            // answer describes the file as it is now rather than whenever something last read a setting.
            _ = app.MapGet(
                "/health/ready",
                static (IDatabaseReadySignal database, LastGoodDlnaOptionsMonitor options) =>
                {
                    _ = options.CurrentValue;

                    var isDatabaseReady = database.IsReady;
                    var isConfigurationValid = options.IsCurrentValid;

                    return isDatabaseReady && isConfigurationValid
                        ? Results.Ok("ready")
                        : Results.Json(
                            new
                            {
                                status = "not ready",
                                database = isDatabaseReady ? "ready" : "no usable schema yet",
                                configuration = isConfigurationValid
                                    ? "valid"
                                    : "invalid - running on the last settings that validated",
                            },
                            statusCode: StatusCodes.Status503ServiceUnavailable);
                })
                .AddEndpointFilter(new RequirePortEndpointFilter(serverOptions.Port));

            // The admin UI, on the admin port only. Its own controller serves media and thumbnails under
            // /admin/media, so no admin page ever references the media port - an operator's browser never
            // learns that the renderer-facing surface exists, and the UI keeps working where only the
            // admin port is reachable.
            // No port filter here: endpoint filters are not applied to Razor component endpoints - it
            // compiles and does nothing, measured. AdminSurfaceMiddleware is what confines these.
            _ = app.MapRazorComponents<Admin.App>()
                .AddInteractiveServerRenderMode();
        }
    }
}
