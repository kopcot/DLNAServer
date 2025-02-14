using DLNAServer.Database.Entities;

namespace DLNAServer.Database.Repositories.Interfaces
{
    public interface IDirectoryRepository : IBaseRepository<DirectoryEntity>
    {
        Task<IEnumerable<DirectoryEntity>> GetAllParentsByDirectoriesIdAsync(IEnumerable<Guid> expectedDirectory, IEnumerable<string> excludeFolders, bool useCachedResult = true);
        Task<IEnumerable<DirectoryEntity>> GetAllParentsByDirectoriesIdAsync(IEnumerable<string> expectedDirectory, IEnumerable<string> excludeFolders, bool useCachedResult = true);
        Task<IEnumerable<string>> GetAllDirectoryFullNamesAsync(bool useCachedResult = true);
        Task<IEnumerable<DirectoryEntity>> GetAllStartingByPathFullNameAsync(string pathFullName, bool useCachedResult = true);
        Task<IEnumerable<DirectoryEntity>> GetAllStartingByPathFullNamesAsync(IEnumerable<string> pathFullNames, bool useCachedResult = true);
        Task<IEnumerable<DirectoryEntity>> GetAllByDirectoryDepthAsync(int depth, bool useCachedResult = true);
        Task<IEnumerable<DirectoryEntity>> GetAllByDirectoryDepthAsync(int depth, int skip, int take, bool useCachedResult = true);
        Task<IEnumerable<DirectoryEntity>> GetAllByPathFullNamesAsync(IEnumerable<string> pathFullNames, bool asNoTracking = false, bool useCachedResult = true);
    }
}
