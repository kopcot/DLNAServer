using Microsoft.Extensions.Caching.Memory;

namespace DLNAServer.Features.Cache
{
    public partial class FileMemoryCacheManager
    {
        [LoggerMessage(1, LogLevel.Debug, "File cached in background done - '{file}'")]
        partial void DebugBackgroundFileCachedDone(string file);
        [LoggerMessage(2, LogLevel.Debug, "File started caching - '{file}'")]
        partial void DebugFileCacheStarted(string file);
        [LoggerMessage(3, LogLevel.Debug, "File caching done - '{file}'")]
        partial void DebugFileCacheDone(string file);
        [LoggerMessage(4, LogLevel.Debug, "File caching finished - '{file}'")]
        partial void DebugFileCacheFinished(string file);
        [LoggerMessage(5, LogLevel.Debug, "File cached before - '{file}'")]
        partial void DebugFileCacheBefore(string file);
        [LoggerMessage(6, LogLevel.Debug, "Wait {delayBeforeGCCollect}sec(s) before start removing file from cache: {key}, reason: {reason}")]
        partial void DebugWaitBeforeRemoveFromCache(int delayBeforeGCCollect, string? key, EvictionReason reason);
        [LoggerMessage(7, LogLevel.Debug, "Started file remove from cache: {key}, reason: {reason}, allocated bytes: {size}")]
        partial void DebugStartedRemoveFromCache(string? key, EvictionReason reason, long size);
        [LoggerMessage(8, LogLevel.Debug, "File removed from cache: {key}, reason: {reason}")]
        partial void DebugRemovedFromCache(string? key, EvictionReason reason);

    }
}
