using DLNAServer.Features.FileWatcher.Interfaces;
using DLNAServer.Features.MediaContent.Interfaces;
using DLNAServer.SOAP.Endpoints.Interfaces;
using DLNAServer.SOAP.Endpoints.Responses.ContentDirectory;
using DLNAServer.SOAP.Endpoints.Responses.ContentDirectory.Mapping;
using System.Buffers;

namespace DLNAServer.SOAP.Endpoints
{
    public class ContentDirectoryService : IContentDirectoryService, IDisposable
    {
        private readonly Lazy<IContentExplorerManager> _contentExplorerLazy;
        private readonly Lazy<IFileWatcherManager> _fileWatcherManagerLazy;
        private readonly ILogger<ContentDirectoryService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private IContentExplorerManager ContentExplorer => _contentExplorerLazy.Value;
        private IFileWatcherManager FileWatcherManager => _fileWatcherManagerLazy.Value;
        private readonly static SemaphoreSlim _browseLock = new(1, 1);

        public ContentDirectoryService(
            Lazy<IContentExplorerManager> contentExplorerLazy,
            Lazy<IFileWatcherManager> fileWatcherManagerLazy,
            ILogger<ContentDirectoryService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _contentExplorerLazy = contentExplorerLazy;
            _fileWatcherManagerLazy = fileWatcherManagerLazy;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<Browse> Browse(string objectID, string browseFlag, string filter, int startingIndex, int requestedCount, string sortCriteria)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
                _logger.LogDebug($"{DateTime.Now:dd/MM/yyyy HH:mm:ss:fff} Browse(ObjectID: {objectID}, BrowseFlag:{browseFlag}, Filter: {filter}, StartingIndex: {startingIndex}, RequestedCount: {requestedCount}, SortCriteria: {sortCriteria})");
            }
            var startTime = DateTime.Now;

            Browse? response = null;

            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"{DateTime.Now} - Started returning browse items: {objectID}");
                }

                _ = await _browseLock.WaitAsync(TimeSpan.FromMinutes(1));

                requestedCount = Math.Min(requestedCount, 100);
                requestedCount = Math.Max(requestedCount, 1);

                (var fileEntities, var directoryEntities, var isRootFolder, var totalMatches) = await ContentExplorer.GetBrowseResultItems(objectID, startingIndex, requestedCount);

                response = new();

                var localIpEndpoint = $"{connection!.LocalIpAddress!.MapToIPv4()}:{connection!.LocalPort!}";

                if (directoryEntities.Length != 0)
                {
                    response.Result.DidlLite.Containers = directoryEntities
                        .Select(directory => directory.MapContainer(localIpEndpoint, isRootFolder))
                        .ToArray();
                }

                if (fileEntities.Length != 0)
                {
                    response.Result.DidlLite.BrowseItems = fileEntities
                        .Select(file => file.MapItem(localIpEndpoint, isRootFolder))
                        .ToArray();
                }

                response.TotalMatches = totalMatches;
                response.NumberReturned = (uint)(response.Result.DidlLite.BrowseItems.Length + response.Result.DidlLite.Containers.Length);
                response.UpdateID = (uint)FileWatcherManager.UpdatesCount;

                _logger.LogInformation($"{DateTime.Now:dd/MM/yyyy HH:mm:ss:fff} - Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort}, ObjectID: {objectID}, RequestedCount: {requestedCount}, Starting index: {startingIndex}, Items: {response.NumberReturned} from {response.TotalMatches}, Start: {startTime:HH:mm:ss:fff}, End: {DateTime.Now:HH:mm:ss:fff}, Duration (ms): {(DateTime.Now - startTime).TotalMilliseconds:0.00}");

                _ = _browseLock.Release();

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"{DateTime.Now} - Finished returning browse items: {objectID}");
                }

                _httpContextAccessor.HttpContext?.Response.RegisterForDispose(this);

                return response;
            }
            catch (Exception ex)
            {
                _ = _browseLock.Release();

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"{DateTime.Now} - Error in returning browse items: {objectID}");
                }

                _logger.LogError(ex, ex.Message, [objectID]);
                return new();
            }
            finally
            {
                response = null;
            }
        }
        public async Task<GetSearchCapabilities> GetSearchCapabilities()
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogInformation($"{DateTime.Now:dd/MM/yyyy HH:mm:ss:fff} GetSearchCapabilities()");

            await Task.CompletedTask;
            return new() { SearchCaps = "*" };
        }
        public async Task<GetSortCapabilities> GetSortCapabilities()
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogInformation($"{DateTime.Now:dd/MM/yyyy HH:mm:ss:fff} GetSortCapabilities()");

            await Task.CompletedTask;
            return new() { SortCaps = "*" };
        }

        public async Task<IsAuthorized> IsAuthorized(string DeviceID)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogInformation($"{DateTime.Now:dd/MM/yyyy HH:mm:ss:fff} IsAuthorized({DeviceID})");

            await Task.CompletedTask;
            return new() { Result = 1 };
        }
        public async Task<GetSystemUpdateID> GetSystemUpdateID()
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogInformation($"{DateTime.Now:dd/MM/yyyy HH:mm:ss:fff} SystemUpdateID()");

            await Task.CompletedTask;
            return new() { Id = (uint)FileWatcherManager.UpdatesCount };
        }

        public async Task<X_GetFeatureList> X_GetFeatureList()
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogInformation($"{DateTime.Now:dd/MM/yyyy HH:mm:ss:fff} X_GetFeatureList()");

            await Task.CompletedTask;
            return new() { FeatureList = [] };
        }

        public async Task<X_SetBookmark> X_SetBookmark(int CategoryType, int RID, string ObjectID, int PosSecond)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            _logger.LogDebug($"{DateTime.Now} Remote IP Address: {connection?.RemoteIpAddress}:{connection?.RemotePort} , Local IP Address: {connection?.LocalIpAddress}:{connection?.LocalPort}");
            _logger.LogInformation($"{DateTime.Now:dd/MM/yyyy HH:mm:ss:fff} X_SetBookmark(CategoryType: {CategoryType}, RID: {RID}, ObjectID: {ObjectID}, PosSecond: {PosSecond})");

            await Task.CompletedTask;
            return new() { };
        }

        #region Dispose
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ContentDirectoryService()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion Dispose
    }
}
