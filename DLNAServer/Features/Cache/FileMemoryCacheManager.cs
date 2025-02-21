using DLNAServer.Configuration;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.Cache.Interfaces;
using DLNAServer.Helpers.Caching;
using DLNAServer.Helpers.Files;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Runtime;

namespace DLNAServer.Features.Cache
{
    public class FileMemoryCacheManager : IFileMemoryCacheManager
    {
        private readonly ILogger<FileMemoryCacheManager> _logger;
        private readonly Lazy<IMemoryCache> _memoryCacheLazy;
        private readonly ServerConfig _serverConfig;
        private IMemoryCache MemoryCache => _memoryCacheLazy.Value;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly static ConcurrentDictionary<string, SemaphoreSlim> cachingFilesInProgress = new();
        private readonly static SemaphoreSlim postEvictionCallbackInProgress = new(1, 1);
        private readonly static TimeSpan defaultExpiration = TimeSpan.FromMinutes(1);

        public FileMemoryCacheManager(
            ServerConfig serverConfig,
            Lazy<IMemoryCache> memoryCacheLazy,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<FileMemoryCacheManager> logger)
        {
            _memoryCacheLazy = memoryCacheLazy;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _serverConfig = serverConfig;
        }
        public void CacheFileInBackground(FileEntity file, TimeSpan? slidingExpiration)
        {
            new Task(async () =>
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    slidingExpiration = slidingExpiration > defaultExpiration
                        ? slidingExpiration
                        : defaultExpiration;

                    (var isCachedSuccessful, var fileMemoryByteWR) = await CacheFileAndReturnAsync(file.FilePhysicalFullPath, slidingExpiration, true);

                    if (file.FileUnableToCache != !isCachedSuccessful)
                    {
                        var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();

                        var fileCached = await fileRepository.GetByIdAsync(file.Id, asNoTracking: false, useCachedResult: true);
                        fileCached!.FileUnableToCache = !isCachedSuccessful;
                        _ = await fileRepository.SaveChangesAsync();
                    }

                    _logger.LogDebug($"{DateTime.Now} - File cached in background done - {file!.FilePhysicalFullPath} ");
                };
            }, creationOptions: TaskCreationOptions.RunContinuationsAsynchronously).Start();
        }
        public async Task<(bool isCachedSuccessful, ReadOnlyMemory<byte>? file)> CacheFileAndReturnAsync(
            string filePath,
            TimeSpan? slidingExpiration,
            bool checkExistingInCache = true)
        {
            var fileLock = cachingFilesInProgress.GetOrAdd(filePath, new SemaphoreSlim(1, 1));

            _logger.LogDebug($"{DateTime.Now} - Started caching file: {filePath}");
            await fileLock.WaitAsync();

            try
            {
                if (checkExistingInCache)
                {
                    (bool isCached, ReadOnlyMemory<byte> file) = GetCheckCachedFile(filePath);
                    if (isCached)
                    {
                        _logger.LogDebug($"{DateTime.Now} - File cached before - {filePath} ");
                        return (isCached, file);
                    }
                }

                FileInfo fileInfo = new(filePath);
                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    return (false, null);
                }
                var cachedData = await FileHelper.ReadFileAsync(filePath, _logger, (long)_serverConfig.MaxSizeOfFileForUseMemoryCacheInMBytes * 1024 * 1024);
                if (cachedData == null)
                {
                    return (false, null);
                }
                GC.AddMemoryPressure(cachedData.Value.Length);

                CacheFileData(filePath, slidingExpiration, cachedData.Value);

                return (true, cachedData.Value);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message, [filePath, ex.StackTrace]);
                return (false, null);
            }
            finally
            {
                _ = fileLock.Release();
                _logger.LogDebug($"{DateTime.Now} - Finished caching file: {filePath}");

                _ = cachingFilesInProgress.Remove(filePath, out _);
            }
        }

        private void CacheFileData(string filePath, TimeSpan? slidingExpiration, ReadOnlyMemory<byte> cachedData)
        {
            try
            {
                long bytesAllocated = cachedData.Length;
                const int delayBeforeGCCollect = 30; // in seconds
                const int delayAfterGCCollect = 1;   // in seconds

                _ = MemoryCache.Set(GetFileCachedKey(filePath), cachedData, new MemoryCacheEntryOptions()
                {
                    Size = cachedData.Length,
                    SlidingExpiration = slidingExpiration,
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12),
                    Priority = CacheItemPriority.Low,
                }
                .RegisterPostEvictionCallback((key, value, reason, state) =>
                {
                    new Task(async () =>
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug($"{DateTime.Now} - Wait {delayBeforeGCCollect}s before start removing file from cache: {(key as string)}, reason: {reason}");
                        }

                        Thread.Sleep(TimeSpan.FromSeconds(delayBeforeGCCollect));

                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug($"{DateTime.Now} - Started file remove from cache: {(key as string)}, reason: {reason}, allocated bytes: {bytesAllocated}");
                        }

                        await postEvictionCallbackInProgress.WaitAsync();

                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.RemoveMemoryPressure(bytesAllocated);
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        Thread.Sleep(TimeSpan.FromSeconds(delayAfterGCCollect));

                        _ = postEvictionCallbackInProgress.Release();

                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug($"{DateTime.Now} - File removed from cache: {(key as string)}, reason: {reason}");
                        }
                    }).Start();
                }));

                MemoryCache.StartEvictCachedKey(
                    GetFileCachedKey(filePath),
                    slidingExpiration.HasValue
                        ? slidingExpiration.Value.Add(TimeSpan.FromSeconds(delayBeforeGCCollect + delayAfterGCCollect))
                        : TimeSpan.FromMinutes(1));

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"{DateTime.Now} - File cached - {filePath} ");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message, [filePath]);
            }
        }
        public (bool isCached, ReadOnlyMemory<byte> file) GetCheckCachedFile(string filePath)
        {
            try
            {
                if (MemoryCache.TryGetValue(GetFileCachedKey(filePath), out ReadOnlyMemory<byte>? fileMemoryByte)
                    && fileMemoryByte != null
                    && fileMemoryByte.HasValue)
                {
                    MemoryCache.StartEvictCachedKey(
                        GetFileCachedKey(filePath),
                        TimeSpan.FromMinutes(_serverConfig.StoreFileInMemoryCacheAfterLoadInMinute));

                    return (true, fileMemoryByte.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message, [filePath, ex.StackTrace]);
            }
            return (false, ReadOnlyMemory<byte>.Empty);
        }
        public void EvictSingleFile(string filePath)
        {
            try
            {
                MemoryCache.Remove(GetFileCachedKey(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message, [filePath, ex.StackTrace]);
            }
        }
        private static string GetFileCachedKey(string filePath)
        {
            return $"{nameof(FileMemoryCacheManager)} {nameof(CacheFileData)} {typeof(byte[]).Name} {filePath}";
        }
        public async Task TerminateAsync()
        {
            cachingFilesInProgress.Clear();

            if (MemoryCache is MemoryCache memoryCache)
            {
                memoryCache.Compact(100);
                memoryCache.Clear();
            }
            MemoryCache.Dispose();

            await Task.CompletedTask;
        }
    }
}
