using DLNAServer.Common;
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
            defaultCacheDuration = TimeSpanValues.Time5min;
            defaultCacheAbsoluteDuration = TimeSpanValues.Time1hour;
            DefaultOrderBy = static (entities) => entities
                .OrderBy(static (f) => f.LC_ThumbnailFilePhysicalFullPath)
                .ThenByDescending(static (f) => f.CreatedInDB);
            DefaultInclude = static (entities) => entities
                .Include(t => t.ThumbnailData);
        }
        public new Task<ThumbnailEntity?> GetByIdAsync(Guid guid, bool useCachedResult)
        {
            return GetSingleWithCacheAsync(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude)
                    .FirstOrDefaultAsync(t => t.Id == guid),
                cacheKey: GetCacheKey<ThumbnailEntity>([guid.ToString()]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public new Task<ThumbnailEntity?> GetByIdAsync(Guid guid, bool asNoTracking, bool useCachedResult)
        {
            return GetSingleWithCacheAsync(
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
        public new Task<ThumbnailEntity?> GetByIdAsync(string guid, bool useCachedResult)
        {
            return Guid.TryParse(guid, out var dbGuid) ? GetByIdAsync(dbGuid, useCachedResult) : Task.FromResult<ThumbnailEntity?>(null);
        }
        public new Task<ThumbnailEntity?> GetByIdAsync(string guid, bool asNoTracking, bool useCachedResult)
        {
            return Guid.TryParse(guid, out var dbGuid) ? GetByIdAsync(dbGuid, asNoTracking, useCachedResult) : Task.FromResult<ThumbnailEntity?>(null);
        }
        public Task<ThumbnailEntity[]> GetAllByPathFullNameAsync(string pathFullName, bool useCachedResult)
        {
            pathFullName = pathFullName.ToLower(culture: System.Globalization.CultureInfo.InvariantCulture);
            var memoryDataResult = GetAllWithCacheAsync(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude)
                    .Where(t => t.LC_ThumbnailFilePhysicalFullPath != null && t.LC_ThumbnailFilePhysicalFullPath.Equals(pathFullName)),
                cacheKey: GetCacheKey<ThumbnailEntity>([pathFullName]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ContinueWith(static (fe) => fe.Result.ToArray());
        }
    }
}
