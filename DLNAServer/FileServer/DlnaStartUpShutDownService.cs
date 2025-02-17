using DLNAServer.Configuration;
using DLNAServer.Database;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.Cache.Interfaces;
using DLNAServer.Features.FileWatcher.Interfaces;
using DLNAServer.Features.MediaContent.Interfaces;
using DLNAServer.Features.MediaProcessors.Interfaces;
using DLNAServer.Types.DLNA;
using DLNAServer.Types.UPNP.Interfaces;

namespace DLNAServer.FileServer
{
    public class DlnaStartUpShutDownService : IHostedService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<DlnaStartUpShutDownService> _logger;
        public DlnaStartUpShutDownService(IServiceScopeFactory serviceScopeFactory, ILogger<DlnaStartUpShutDownService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                await ShowGeneralInfo(scope);
                await InitDatabase(scope, cancellationToken);
                await InitUPNPDevices(scope);
                await InitContentExplorer(scope);
                await InitAudioProcessor(scope);
                await InitVideoProcessor(scope);
                await InitImageProcessor(scope);
                await InitFileWatcherManager(scope);
            };
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                await TerminateFileMemoryCacheManager(scope);
                await TerminateFileWatcherHandler(scope);
                await TerminateContentExplorer(scope);
                await TerminateDatabase(scope);
                await TerminateUPNPDevices(scope);
                await TerminateFileWatcherManager(scope);
                await TerminateAudioProcessor(scope);
                await TerminateVideoProcessor(scope);
                await TerminateImageProcessor(scope);
            };
        }
        #region StartUp 

        private async Task InitAudioProcessor(IServiceScope scope)
        {
            var audioProcessor = scope.ServiceProvider.GetRequiredService<IAudioProcessor>();
            await audioProcessor.InitializeAsync();
            _logger.LogInformation($"Audio processor - Initialized");
        }
        private async Task InitVideoProcessor(IServiceScope scope)
        {
            var videoProcessor = scope.ServiceProvider.GetRequiredService<IVideoProcessor>();
            await videoProcessor.InitializeAsync();
            _logger.LogInformation($"Video processor - Initialized");
        }
        private async Task InitImageProcessor(IServiceScope scope)
        {
            var imageProcessor = scope.ServiceProvider.GetRequiredService<IImageProcessor>();
            await imageProcessor.InitializeAsync();
            _logger.LogInformation($"Image processor - Initialized");
        }

        private async Task InitContentExplorer(IServiceScope scope)
        {
            var contentExplorer = scope.ServiceProvider.GetRequiredService<IContentExplorerManager>();
            await contentExplorer.InitializeAsync();
            _logger.LogInformation($"Content explorer - Initialized");
        }

        private async Task InitUPNPDevices(IServiceScope scope)
        {
            var uPNPDevices = scope.ServiceProvider.GetRequiredService<IUPNPDevices>();
            await uPNPDevices.InitializeAsync();
            _logger.LogInformation($"UPNPDevices Devices - Initialized");
        }

        private async Task ShowGeneralInfo(IServiceScope scope)
        {
            var serverConfig = scope.ServiceProvider.GetRequiredService<ServerConfig>();
            _logger.LogInformation($"Dlna server name: {serverConfig.ServerFriendlyName}");
            _logger.LogInformation($"Source folders: {string.Join(";", serverConfig.SourceFolders)}");
            _logger.LogInformation($"Extensions: {string.Join(";", serverConfig.MediaFileExtensions.Select(e => (e.Key, e.Value.Key.ToMimeString(), e.Value.Value)).ToArray())}");

            await Task.CompletedTask;
        }
        private async Task InitDatabase(IServiceScope scope, CancellationToken cancellationToken)
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DlnaDbContext>();
            var serverRepository = scope.ServiceProvider.GetRequiredService<IServerRepository>();
            var serverConfig = scope.ServiceProvider.GetRequiredService<ServerConfig>();
            bool isDbOk = true;
            try
            {
                _ = await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
                var lastMachineName = await serverRepository.GetLastAccessMachineNameAsync();
                _logger.LogInformation($"ActualMachineName = '{Environment.MachineName}', LastMachineName = '{lastMachineName}'");
                isDbOk &= lastMachineName == Environment.MachineName;
                isDbOk &= await dbContext.CheckDbSetsOk(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                isDbOk = false;
            }

            if (!isDbOk || serverConfig.ServerAlwaysRecreateDatabaseAtStart)
            {
                _ = await dbContext.Database.EnsureDeletedAsync(cancellationToken); // Delete existing database
                _ = await dbContext.Database.EnsureCreatedAsync(cancellationToken); // Recreate the database
                _ = await serverRepository.AddAsync(new ServerEntity()
                {
                    LasAccess = DateTime.Now,
                    MachineName = Environment.MachineName,
                });
                _logger.LogWarning($"Database cleared!!!");
            }
            try
            {
                _ = await dbContext.OptimizeDatabase(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

            _logger.LogInformation($"Database - Initialized");
        }
        private async Task InitFileWatcherManager(IServiceScope scope)
        {
            var fileWatcherManager = scope.ServiceProvider.GetRequiredService<IFileWatcherManager>();
            await fileWatcherManager.InitializeAsync();
            _logger.LogInformation($"FileWatcherManager Devices - Initialized");
        }

        #endregion

        #region ShutDown
        private async Task TerminateFileMemoryCacheManager(IServiceScope scope)
        {
            var fileMemoryCacheManager = scope.ServiceProvider.GetRequiredService<IFileMemoryCacheManager>();
            await fileMemoryCacheManager.TerminateAsync();
            _logger.LogDebug($"File memory cache - Terminated");
        }
        private async Task TerminateUPNPDevices(IServiceScope scope)
        {
            var uPNPDevices = scope.ServiceProvider.GetRequiredService<IUPNPDevices>();
            await uPNPDevices.TerminateAsync();
            _logger.LogDebug($"UPNPDevices Devices - Terminated");
        }
        private async Task TerminateFileWatcherHandler(IServiceScope scope)
        {
            var fileWatcherHandler = scope.ServiceProvider.GetRequiredService<IFileWatcherHandler>();
            await fileWatcherHandler.TerminateAsync();
            _logger.LogDebug($"File watcher handler - Terminated");
        }
        private async Task TerminateContentExplorer(IServiceScope scope)
        {
            var contentExplorerManager = scope.ServiceProvider.GetRequiredService<IContentExplorerManager>();
            await contentExplorerManager.TerminateAsync();
            _logger.LogDebug($"Content explorer  - Terminated");
        }
        private async Task TerminateDatabase(IServiceScope scope)
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DlnaDbContext>();
            await dbContext.TerminateAsync();
            _logger.LogDebug($"Database - Terminated");
        }
        private async Task TerminateFileWatcherManager(IServiceScope scope)
        {
            var fileWatcherManager = scope.ServiceProvider.GetRequiredService<IFileWatcherManager>();
            await fileWatcherManager.TerminateAsync();
            _logger.LogDebug($"FileWatcherManager - Terminated");
        }
        private async Task TerminateAudioProcessor(IServiceScope scope)
        {
            var audioProcessor = scope.ServiceProvider.GetRequiredService<IAudioProcessor>();
            await audioProcessor.TerminateAsync();
            _logger.LogDebug($"AudioProcessor - Terminated");
        }
        private async Task TerminateVideoProcessor(IServiceScope scope)
        {
            var videoProcessor = scope.ServiceProvider.GetRequiredService<IVideoProcessor>();
            await videoProcessor.TerminateAsync();
            _logger.LogDebug($"VideoProcessor - Terminated");
        }
        private async Task TerminateImageProcessor(IServiceScope scope)
        {
            var imageProcessor = scope.ServiceProvider.GetRequiredService<IImageProcessor>();
            await imageProcessor.TerminateAsync();
            _logger.LogDebug($"ImageProcessor - Terminated");
        }
        #endregion
    }
}
