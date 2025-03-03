using DLNAServer.Common;
using DLNAServer.Features.FileWatcher.Interfaces;
using DLNAServer.Features.MediaContent.Interfaces;
using DLNAServer.Helpers.Logger;
using DLNAServer.SOAP.Endpoints.Interfaces;
using DLNAServer.SOAP.Endpoints.Responses.ContentDirectory;
using DLNAServer.SOAP.Endpoints.Responses.ContentDirectory.Mapping;

namespace DLNAServer.SOAP.Endpoints
{
    public partial class ContentDirectoryService : IContentDirectoryService, IDisposable
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

            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(Browse),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            DebugBrowseRequestInfo(
                nameof(Browse),
                objectID,
                browseFlag,
                filter,
                startingIndex,
                requestedCount,
                sortCriteria);

            var startTime = DateTime.Now;

            Browse? response = null;

            try
            {
                DebugBrowseRequestStart(objectID);

                _ = await _browseLock.WaitAsync(TimeSpanValues.Time1min);

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

                InformationStartBrowseRequest(
                    connection?.RemoteIpAddress,
                    connection?.RemotePort,
                    objectID,
                    requestedCount,
                    startingIndex,
                    response.NumberReturned,
                    response.TotalMatches,
                    (DateTime.Now - startTime).TotalMilliseconds
                    );

                _ = _browseLock.Release();

                DebugBrowseRequestFinish(objectID);

                _httpContextAccessor.HttpContext?.Response.RegisterForDispose(this);

                return response;
            }
            catch (Exception ex)
            {
                _ = _browseLock.Release();

                DebugBrowseRequestError(objectID);

                _logger.LogGeneralErrorMessage(ex);
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
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(GetSearchCapabilities),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            LoggerHelper.LogGeneralInformationMessage(_logger, nameof(GetSearchCapabilities));

            await Task.CompletedTask;
            return new() { SearchCaps = "*" };
        }
        public async Task<GetSortCapabilities> GetSortCapabilities()
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(GetSortCapabilities),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            // not found operation in real usage
            LoggerHelper.LogGeneralWarningMessage(_logger, nameof(GetSortCapabilities));

            await Task.CompletedTask;
            return new() { SortCaps = "*" };
        }

        public async Task<IsAuthorized> IsAuthorized(string DeviceID)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(IsAuthorized),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            // not found operation in real usage
            LoggerHelper.LogGeneralWarningMessage(_logger, nameof(IsAuthorized));

            await Task.CompletedTask;
            return new() { Result = 1 };
        }
        public async Task<GetSystemUpdateID> GetSystemUpdateID()
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(GetSystemUpdateID),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            // not found operation in real usage
            LoggerHelper.LogGeneralWarningMessage(_logger, nameof(GetSystemUpdateID));

            await Task.CompletedTask;
            return new() { Id = (uint)FileWatcherManager.UpdatesCount };
        }

        public async Task<X_GetFeatureList> X_GetFeatureList()
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(X_GetFeatureList),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            // not found operation in real usage
            LoggerHelper.LogGeneralWarningMessage(_logger, nameof(X_GetFeatureList));

            await Task.CompletedTask;
            return new() { FeatureList = [] };
        }

        public async Task<X_SetBookmark> X_SetBookmark(int CategoryType, int RID, string ObjectID, int PosSecond)
        {
            var connection = _httpContextAccessor.HttpContext?.Connection;
            LoggerHelper.LogDebugConnectionInformation(
                _logger,
                nameof(X_SetBookmark),
                connection?.RemoteIpAddress,
                connection?.RemotePort,
                connection?.LocalIpAddress,
                connection?.LocalPort,
                _httpContextAccessor.HttpContext?.Request.Path.Value,
                _httpContextAccessor.HttpContext?.Request.Method);
            // not found operation in real usage
            WarningX_SetBookmarkRequestInfo(
                nameof(X_SetBookmark),
                CategoryType,
                RID,
                ObjectID,
                PosSecond
            );

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
