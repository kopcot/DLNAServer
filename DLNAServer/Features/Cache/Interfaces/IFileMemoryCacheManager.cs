using DLNAServer.Database.Entities;
using DLNAServer.Helpers.Interfaces;

namespace DLNAServer.Features.Cache.Interfaces
{
    public interface IFileMemoryCacheManager : ITerminateAble
    {
        void CacheFileInBackground(FileEntity file, TimeSpan? slidingExpiration);
        Task<(bool isCachedSuccessful, ReadOnlyMemory<byte> file)> CacheFileAndReturnAsync(
            string filePath,
            TimeSpan? slidingExpiration,
            bool checkExistingInCache = true);
        (bool isCached, ReadOnlyMemory<byte> file) GetCheckCachedFile(string filePath, TimeSpan? slidingExpiration);
        void EvictSingleFile(string filePath);
    }
}
