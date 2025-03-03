using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Helpers.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DLNAServer.Database.Repositories
{

    public class DirectoryRepository : BaseRepository<DirectoryEntity>, IDirectoryRepository
    {
        public DirectoryRepository(DlnaDbContext dbContext, Lazy<IMemoryCache> memoryCacheLazy) : base(dbContext, memoryCacheLazy, nameof(DirectoryRepository))
        {
            DefaultOrderBy = static (entities) => entities
                .OrderBy(static (d) => d.LC_DirectoryFullPath)
                .ThenByDescending(static (d) => d.CreatedInDB);
            DefaultInclude = static (entities) => entities
                .Include(d => d.ParentDirectory);
        }
        public new Task<DirectoryEntity[]> GetAllAsync(bool useCachedResult)
        {
            var memoryDataResult = GetAllWithCacheAsync(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude),
                cacheKey: GetCacheKey<DirectoryEntity>(),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ContinueWith(static (de) => de.Result.ToArray());
        }
        public Task<DirectoryEntity[]> GetAllParentsByDirectoriesIdAsync(IEnumerable<Guid> expectedDirectories, IEnumerable<string> excludeFolders, bool useCachedResult)
        {
            var exclude = excludeFolders.Select(ef => ef.ToLower(culture: System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var memoryDataResult = GetAllWithCacheAsync(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude)
                    .Where(d => d.ParentDirectory != null
                        && expectedDirectories.Contains(d.ParentDirectory.Id)
                        && exclude.All(ef => !d.LC_DirectoryFullPath.Contains(ef))),
                cacheKey: GetCacheKey<DirectoryEntity>(expectedDirectories.Select(static (e) => e.ToString())),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ContinueWith(static (de) => de.Result.ToArray());
        }
        public Task<DirectoryEntity[]> GetAllParentsByDirectoriesIdAsync(IEnumerable<string> expectedDirectories, IEnumerable<string> excludeFolders, bool useCachedResult)
        {
            return GetAllParentsByDirectoriesIdAsync(expectedDirectories.Select(static (ed) => Guid.TryParse(ed, out var dbGuid) ? dbGuid : new Guid()), excludeFolders, useCachedResult);
        }
        public Task<string[]> GetAllDirectoryFullNamesAsync(bool useCachedResult)
        {
            var memoryDataResult = GetAllWithCacheAsync(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .AsNoTracking()
                    .Select(static (d) => d.DirectoryFullPath),
                cacheKey: GetCacheKey<string>(),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ContinueWith(static (de) => de.Result.ToArray());
        }
        public Task<DirectoryEntity[]> GetAllByDirectoryDepthAsync(int depth, bool useCachedResult)
        {
            var memoryDataResult = GetAllWithCacheAsync(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude)
                    .Where(d => d.Depth == depth),
                cacheKey: GetCacheKey<DirectoryEntity>([depth.ToString()]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ContinueWith(static (de) => de.Result.ToArray());

        }
        public Task<DirectoryEntity[]> GetAllByDirectoryDepthAsync(int depth, int skip, int take, bool useCachedResult)
        {
            var memoryDataResult = GetAllWithCacheAsync(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude)
                    .Where(d => d.Depth == depth),
                cacheKey: GetCacheKey<DirectoryEntity>([depth.ToString(), skip.ToString(), take.ToString()]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ContinueWith(static (de) => de.Result.ToArray());
        }
        public Task<DirectoryEntity[]> GetAllStartingByPathFullNameAsync(string pathFullName, bool useCachedResult)
        {
            pathFullName = pathFullName.ToLower(culture: System.Globalization.CultureInfo.InvariantCulture);
            var memoryDataResult = GetAllWithCacheAsync(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude)
                    .Where(d => d.LC_DirectoryFullPath == pathFullName
                        || d.LC_DirectoryFullPath.StartsWith(pathFullName + Path.DirectorySeparatorChar)),
                cacheKey: GetCacheKey<DirectoryEntity>([pathFullName]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ContinueWith(static (de) => de.Result.ToArray());
        }
        public Task<DirectoryEntity[]> GetAllStartingByPathFullNamesAsync(IEnumerable<string> pathFullNames, bool useCachedResult)
        {
            pathFullNames = pathFullNames.Select(static (p) => p.ToLower(culture: System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var memoryDataResult = GetAllWithCacheAsync(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude)
                    .Where(d => pathFullNames.Contains(d.LC_DirectoryFullPath)
                        || pathFullNames.Any(p => d.LC_DirectoryFullPath.StartsWith(p + Path.DirectorySeparatorChar))),
                cacheKey: GetCacheKey<DirectoryEntity>(pathFullNames),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ContinueWith(static (de) => de.Result.ToArray());
        }
        public Task<DirectoryEntity[]> GetAllByPathFullNamesAsync(IEnumerable<string> pathFullNames, bool asNoTracking, bool useCachedResult)
        {
            pathFullNames = pathFullNames.Select(static (p) => p.ToLower(culture: System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var memoryDataResult = GetAllWithCacheAsync(
                queryAction: asNoTracking
                    ? DbSet
                        .OrderEntitiesByDefault(DefaultOrderBy)
                        .IncludeChildEntities(DefaultInclude)
                        .AsNoTracking()
                        .Where(d => pathFullNames.Contains(d.LC_DirectoryFullPath))
                    : DbSet
                        .OrderEntitiesByDefault(DefaultOrderBy)
                        .IncludeChildEntities(DefaultInclude)
                        .Where(d => pathFullNames.Contains(d.LC_DirectoryFullPath)),
                cacheKey: GetCacheKey<DirectoryEntity>(pathFullNames),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ContinueWith(static (de) => de.Result.ToArray());
        }
    }
}
