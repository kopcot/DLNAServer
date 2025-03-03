using DLNAServer.Database.Entities;

namespace DLNAServer.Database.Repositories.Interfaces
{
    public interface IDirectoryRepository : IBaseRepository<DirectoryEntity>
    {
        Task<DirectoryEntity[]> GetAllParentsByDirectoriesIdAsync(IEnumerable<Guid> expectedDirectories, IEnumerable<string> excludeFolders, bool useCachedResult = true);
        Task<DirectoryEntity[]> GetAllParentsByDirectoriesIdAsync(IEnumerable<string> expectedDirectories, IEnumerable<string> excludeFolders, bool useCachedResult = true);
        Task<string[]> GetAllDirectoryFullNamesAsync(bool useCachedResult = true);
        Task<DirectoryEntity[]> GetAllStartingByPathFullNameAsync(string pathFullName, bool useCachedResult = true);
        Task<DirectoryEntity[]> GetAllStartingByPathFullNamesAsync(IEnumerable<string> pathFullNames, bool useCachedResult = true);
        Task<DirectoryEntity[]> GetAllByDirectoryDepthAsync(int depth, bool useCachedResult = true);
        Task<DirectoryEntity[]> GetAllByDirectoryDepthAsync(int depth, int skip, int take, bool useCachedResult = true);
        Task<DirectoryEntity[]> GetAllByPathFullNamesAsync(IEnumerable<string> pathFullNames, bool asNoTracking = false, bool useCachedResult = true);
    }
}
