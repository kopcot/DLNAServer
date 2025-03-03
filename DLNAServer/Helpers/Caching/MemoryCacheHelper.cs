using DLNAServer.Common;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace DLNAServer.Helpers.Caching
{
    public static class MemoryCacheHelper
    {
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> EvictionControlTokens = new();

        /// <summary>
        /// Start a <see cref="Task"/> to evict <paramref name="cacheKey"/> from <see cref="IMemoryCache"/>.<br/>
        /// <paramref name="delayEviction"/> should be greater as value <see cref="MemoryCacheEntryOptions.SlidingExpiration"/>.<br/>
        /// If <see cref="MemoryCacheEntryOptions.PostEvictionCallbacks"/> is used, consider potential delays caused by their execution 
        /// and/or added delays there
        /// </summary> 
        /// <param name="memoryCache">The memory cache instance.</param>
        /// <param name="cacheKey">The cache key to be evicted.</param>
        /// <param name="delayEviction">The delay before attempting eviction.</param>
        public static void StartEvictCachedKey(this IMemoryCache memoryCache, string cacheKey, TimeSpan delayEviction)
        {

            string evictionCacheKey = string.Format("_{0} {1}", [string.Intern(nameof(StartEvictCachedKey)), string.Intern(cacheKey)]);

            var evictionControlTokensSource = EvictionControlTokens.AddOrUpdate(
                key: evictionCacheKey,
                addValue: new CancellationTokenSource(),
                updateValueFactory: (key, existingCts) =>
                {
                    try
                    {
                        existingCts.Cancel();
                        existingCts.Dispose();
                    }
                    catch { }
                    return new CancellationTokenSource();
                });

            new Task(async () =>
            {
                try
                {
                    var delay = GetDelay(delayEviction);

                    memoryCache.Remove(evictionCacheKey);

                    await Task.Delay(delay, evictionControlTokensSource.Token);

                    if (!evictionControlTokensSource.Token.IsCancellationRequested)
                    {
                        const object? storeValue = null;
                        _ = memoryCache.Set(
                            key: evictionCacheKey,
                            value: new WeakReference(storeValue),
                            options: memoryCacheEntryOptions);
                    }

                    await Task.Delay(memoryCacheEntryOptions.SlidingExpiration!.Value, evictionControlTokensSource.Token);

                    if (!evictionControlTokensSource.Token.IsCancellationRequested)
                    {
                        memoryCache.Remove(evictionCacheKey);
                    }

                }
                catch { }
                finally
                {
                    _ = EvictionControlTokens.TryRemove(evictionCacheKey, out evictionControlTokensSource);
                    try
                    {
                        evictionControlTokensSource?.Cancel();
                        evictionControlTokensSource?.Dispose();
                    }
                    catch { }
                }
            }, creationOptions: TaskCreationOptions.RunContinuationsAsynchronously).Start();
        }
        private static TimeSpan GetDelay(TimeSpan cacheDuration)
        {
            const double minAddedSeconds = 2.0;
            const double maxAddedSeconds = 10.0;

            var addedSeconds = Math.Max(Math.Min(cacheDuration.TotalSeconds / 2, maxAddedSeconds), minAddedSeconds);

            return cacheDuration.Add(TimeSpan.FromSeconds(addedSeconds));
        }

        private static readonly MemoryCacheEntryOptions memoryCacheEntryOptions = new()
        {
            Size = 1,
            SlidingExpiration = TimeSpanValues.Time10sec,
            AbsoluteExpirationRelativeToNow = TimeSpanValues.Time10min,
            Priority = CacheItemPriority.Low
        };
    }
}
