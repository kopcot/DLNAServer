using DLNAServer.Database.Entities;
using DLNAServer.Helpers.Interfaces;

namespace DLNAServer.Features.Cache.Interfaces
{
    public interface IFileMemoryCacheManager : ITerminateAble
    {
        void CacheFileInBackground(FileEntity file, TimeSpan? slidingExpiration);
        Task<(bool isCachedSuccessful, WeakReference<byte[]>? file)> CacheFileAndReturnAsync(
            string filePath,
            TimeSpan? slidingExpiration,
            bool checkExistingInCache = true);
        Task<(bool isCached, byte[] file)> GetCheckCachedFileAsync(string filePath);
        void EvictSingleFile(string filePath);
    }
}
