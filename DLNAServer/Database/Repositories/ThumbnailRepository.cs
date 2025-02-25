using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Helpers.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DLNAServer.Database.Repositories
{
    public class ThumbnailRepository : BaseRepository<ThumbnailEntity>, IThumbnailRepository
    {
        public ThumbnailRepository(DlnaDbContext dbContext, Lazy<IMemoryCache> memoryCacheLazy) : base(dbContext, memoryCacheLazy, nameof(ThumbnailRepository))
        {
            defaultCacheDuration = TimeSpan.FromMinutes(5);
            defaultCacheAbsoluteDuration = TimeSpan.FromHours(1);
            DefaultOrderBy = static (entities) => entities
                .OrderBy(static (f) => f.LC_ThumbnailFilePhysicalFullPath)
                .ThenByDescending(static (f) => f.CreatedInDB);
            DefaultInclude = static (entities) => entities
                .Include(t => t.ThumbnailData);
        }
        public new async Task<ThumbnailEntity?> GetByIdAsync(Guid guid, bool useCachedResult)
        {
            return await GetSingleWithCacheAsync(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude) 
                    .FirstOrDefaultAsync(t => t.Id == guid),
                cacheKey: GetCacheKey<ThumbnailEntity>([guid.ToString()]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public new async Task<ThumbnailEntity?> GetByIdAsync(Guid guid, bool asNoTracking, bool useCachedResult)
        {
            return await GetSingleWithCacheAsync(
                queryAction: asNoTracking
                        ? DbSet
                            .OrderEntitiesByDefault(DefaultOrderBy)
                            .IncludeChildEntities(DefaultInclude)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t => t.Id == guid)
                        : DbSet
                            .OrderEntitiesByDefault(DefaultOrderBy)
                            .IncludeChildEntities(DefaultInclude)
                            .FirstOrDefaultAsync(t => t.Id == guid),
                cacheKey: GetCacheKey<ThumbnailEntity>([asNoTracking.ToString(), guid.ToString()]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public new async Task<ThumbnailEntity?> GetByIdAsync(string guid, bool useCachedResult)
        {
            return Guid.TryParse(guid, out var dbGuid) ? await GetByIdAsync(dbGuid, useCachedResult) : null;
        }
        public new async Task<ThumbnailEntity?> GetByIdAsync(string guid, bool asNoTracking, bool useCachedResult)
        {
            return Guid.TryParse(guid, out var dbGuid) ? await GetByIdAsync(dbGuid, asNoTracking, useCachedResult) : null;
        }
        public async Task<ThumbnailEntity[]> GetAllByPathFullNameAsync(string pathFullName, bool useCachedResult)
        {
            pathFullName = pathFullName.ToLower();
            var memoryDataResult = await GetAllWithCacheAsync(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude)
                    .Where(t => t.LC_ThumbnailFilePhysicalFullPath != null && t.LC_ThumbnailFilePhysicalFullPath.Equals(pathFullName)),
                cacheKey: GetCacheKey<ThumbnailEntity>([pathFullName]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ToArray();
        }
    }
}
