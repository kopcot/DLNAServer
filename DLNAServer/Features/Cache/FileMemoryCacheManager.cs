using DLNAServer.Common;
using DLNAServer.Configuration;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.Cache.Interfaces;
using DLNAServer.Helpers.Caching;
using DLNAServer.Helpers.Files;
using DLNAServer.Helpers.Logger;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Runtime;

namespace DLNAServer.Features.Cache
{
    public partial class FileMemoryCacheManager : IFileMemoryCacheManager
    {
        private readonly ILogger<FileMemoryCacheManager> _logger;
        private readonly Lazy<IMemoryCache> _memoryCacheLazy;
        private readonly ServerConfig _serverConfig;
        private IMemoryCache MemoryCache => _memoryCacheLazy.Value;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly static ConcurrentDictionary<string, SemaphoreSlim> cachingFilesInProgress = new();
        private readonly static SemaphoreSlim postEvictionCallbackInProgress = new(1, 1);
        private readonly static TimeSpan defaultExpiration = TimeSpanValues.Time1min;

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
            Task backgroundCaching = new(async () =>
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    slidingExpiration = slidingExpiration > defaultExpiration
                        ? slidingExpiration
                        : defaultExpiration;

                    (var isCachedSuccessful, _) = await CacheFileAndReturnAsync(file.FilePhysicalFullPath, slidingExpiration, true);

                    if (file.FileUnableToCache != !isCachedSuccessful)
                    {
                        var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();

                        var fileCached = await fileRepository.GetByIdAsync(file.Id, asNoTracking: false, useCachedResult: true);
                        fileCached!.FileUnableToCache = !isCachedSuccessful;
                        _ = await fileRepository.SaveChangesAsync();
                    }

                    DebugBackgroundFileCachedDone(file!.FilePhysicalFullPath);
                }
            }, creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
            backgroundCaching.Start();
        }
        public async Task<(bool isCachedSuccessful, ReadOnlyMemory<byte> file)> CacheFileAndReturnAsync(
            string filePath,
            TimeSpan? slidingExpiration,
            bool checkExistingInCache = true)
        {
            var fileLock = cachingFilesInProgress.GetOrAdd(filePath, new SemaphoreSlim(1, 1));

            DebugFileCacheStarted(filePath);
            _ = await fileLock.WaitAsync(TimeSpanValues.Time30min);

            try
            {
                if (checkExistingInCache)
                {
                    (bool isCached, ReadOnlyMemory<byte> file) = GetCheckCachedFile(filePath, slidingExpiration);
                    if (isCached)
                    {
                        DebugFileCacheBefore(filePath);
                        return (isCached, file);
                    }
                }

                FileInfo fileInfo = new(filePath);
                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    return (false, ReadOnlyMemory<byte>.Empty);
                }
                var cachedData = await FileHelper.ReadFileAsync(filePath, _logger, (long)_serverConfig.MaxSizeOfFileForUseMemoryCacheInMBytes * 1024 * 1024);
                if (cachedData == null)
                {
                    return (false, ReadOnlyMemory<byte>.Empty);
                }
                GC.AddMemoryPressure(cachedData.Value.Length);

                var cachedDataMemory = cachedData.Value.ToArray();

                CacheFileData(ref filePath, ref slidingExpiration, ref cachedDataMemory);

                return (true, cachedDataMemory);

            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
                return (false, ReadOnlyMemory<byte>.Empty);
            }
            finally
            {
                _ = fileLock.Release();

                DebugFileCacheFinished(filePath);

                _ = cachingFilesInProgress.Remove(filePath, out _);
            }
        }
        private const int delayBeforeGCCollect = 30; // in seconds
        private const int delayAfterGCCollect = 1;   // in seconds
        private void CacheFileData(ref readonly string filePath, ref readonly TimeSpan? slidingExpiration, ref byte[] cachedData)
        {
            try
            {
                long bytesAllocated = cachedData.Length;
                _ = MemoryCache.Set(GetFileCachedKey(filePath), cachedData, entryOptions(cachedData.Length, slidingExpiration));

                MemoryCache.StartEvictCachedKey(
                    GetFileCachedKey(filePath),
                    // doubled slidingExpiration for streaming file
                    slidingExpiration?.Add(slidingExpiration.Value) ?? TimeSpanValues.Time1min);

                DebugFileCacheDone(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
            }
        }
        public (bool isCached, ReadOnlyMemory<byte> file) GetCheckCachedFile(string filePath, TimeSpan? slidingExpiration)
        {
            try
            {
                if (MemoryCache.TryGetValue(GetFileCachedKey(filePath), out byte[]? fileMemoryByte)
                    && fileMemoryByte != null)
                {
                    MemoryCache.StartEvictCachedKey(
                        GetFileCachedKey(filePath),
                        // doubled slidingExpiration for streaming file
                        slidingExpiration?.Add(slidingExpiration.Value) ?? TimeSpanValues.Time1min);

                    return (true, fileMemoryByte);
                }
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
            }
            return (false, ReadOnlyMemory<byte>.Empty);
        }

        private static readonly Func<long, TimeSpan?, MemoryCacheEntryOptions> entryOptions = (size, slidingExpiration) =>
        {
            return new MemoryCacheEntryOptions()
            {
                Size = size,
                SlidingExpiration = slidingExpiration,
                AbsoluteExpirationRelativeToNow = TimeSpanValues.Time12hour,
                Priority = CacheItemPriority.Low,
            }
            .RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                Task clearMemory = new(async () =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(delayBeforeGCCollect));

                    _ = await postEvictionCallbackInProgress.WaitAsync(TimeSpanValues.Time30min);

                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.RemoveMemoryPressure(size);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    Thread.Sleep(TimeSpan.FromSeconds(delayAfterGCCollect));

                    _ = postEvictionCallbackInProgress.Release();

                });
                clearMemory.Start();
            });
        };
        public void EvictSingleFile(string filePath)
        {
            try
            {
                MemoryCache.Remove(GetFileCachedKey(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
            }
        }
        private static string GetFileCachedKey(string filePath)
        {
            return string.Format("{0} {1} {2} {3}", [nameof(FileMemoryCacheManager), nameof(CacheFileData), typeof(byte[]).Name, filePath]);
        }
        public Task TerminateAsync()
        {
            cachingFilesInProgress.Clear();

            if (MemoryCache is MemoryCache memoryCache)
            {
                memoryCache.Compact(100);
                memoryCache.Clear();
            }
            MemoryCache.Dispose();

            return Task.CompletedTask;
        }
    }
}
