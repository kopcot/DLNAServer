using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Helpers.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DLNAServer.Database.Repositories
{
    public class FileRepository : BaseRepository<FileEntity>, IFileRepository
    {
        public FileRepository(DlnaDbContext dbContext, Lazy<IMemoryCache> memoryCacheLazy) : base(dbContext, memoryCacheLazy, nameof(FileRepository))
        {
            DefaultOrderBy = static (entities) => entities
                .OrderBy(static (f) => f.LC_FilePhysicalFullPath)
                .ThenByDescending(static (f) => f.CreatedInDB);
            DefaultInclude = static (entities) => entities
                        .Include(f => f.Directory)
                        .Include(f => f.AudioMetadata)
                        .Include(f => f.VideoMetadata)
                        .Include(f => f.SubtitleMetadata)
                        .Include(f => f.Thumbnail)
                        .AsSplitQuery();
        }
        public new async Task<bool> AddAsync(FileEntity entity)
        {
            return await AddRangeAsync([entity]);
        }
        public new async Task<bool> AddRangeAsync(IEnumerable<FileEntity> entities)
        {
            DbContext.AttachRange(
                entities
                    .Where(e => e.Directory != null)
                    .Select(e => e.Directory!)
                    .ToArray());
            DbContext.AttachRange(
                entities
                    .Where(e => e.AudioMetadata != null)
                    .Select(e => e.AudioMetadata!)
                    .ToArray());
            DbContext.AttachRange(
                entities
                    .Where(e => e.VideoMetadata != null)
                    .Select(e => e.VideoMetadata!)
                    .ToArray());
            DbContext.AttachRange(
                entities
                    .Where(e => e.SubtitleMetadata != null)
                    .Select(e => e.SubtitleMetadata!)
                    .ToArray());
            DbContext.AttachRange(
                entities
                    .Where(e => e.Thumbnail != null)
                    .Select(e => e.Thumbnail!)
                    .ToArray());
            DbContext.AttachRange(
                entities
                    .Where(e => e.Thumbnail?.ThumbnailData != null)
                    .Select(e => e.Thumbnail!.ThumbnailData!)
                    .ToArray());

            return await base.AddRangeAsync(entities);
        }
        public new async Task<IEnumerable<FileEntity>> GetAllAsync(bool useCachedResult)
        {
            return await GetAllWithCacheAsync<FileEntity>(
                queryAction: DbSet
                        .OrderEntitiesByDefault(DefaultOrderBy)
                        .IncludeChildEntities(DefaultInclude),
                cacheKey: GetCacheKey<FileEntity>(),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public new async Task<IEnumerable<FileEntity>> GetAllAsync(bool asNoTracking, bool useCachedResult)
        {
            return await GetAllWithCacheAsync<FileEntity>(
                queryAction: asNoTracking
                    ? DbSet
                        .OrderEntitiesByDefault(DefaultOrderBy)
                        .IncludeChildEntities(DefaultInclude)
                        .AsNoTracking()
                    : DbSet
                        .OrderEntitiesByDefault(DefaultOrderBy)
                        .IncludeChildEntities(DefaultInclude),
                cacheKey: GetCacheKey<FileEntity>(),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public new async Task<IEnumerable<FileEntity>> GetAllAsync(int skip, int take, bool useCachedResult)
        {
            return await GetAllWithCacheAsync<FileEntity>(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude)
                    .Skip(skip)
                    .Take(take),
                cacheKey: GetCacheKey<FileEntity>([skip.ToString(), take.ToString()]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public async Task<IEnumerable<FileEntity>> GetAllByAddedToDbAsync(int takeNumber, IEnumerable<string> excludeFolders, bool useCachedResult)
        {
            var exclude = excludeFolders.Select(ef => ef.ToLower()).ToArray();
            return await GetAllWithCacheAsync<FileEntity>(
                queryAction: DbSet
                    .OrderByDescending(static (f) => f.CreatedInDB)
                    .IncludeChildEntities(DefaultInclude)
                    .Where(fe => exclude.All(ef => !fe.LC_FilePhysicalFullPath.Contains(ef)))
                    .Take(takeNumber),
                cacheKey: GetCacheKey<FileEntity>([takeNumber.ToString()]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public async Task<IEnumerable<FileEntity>> GetAllByParentDirectoryIdsAsync(IEnumerable<Guid> expectedDirectory, IEnumerable<string> excludeFolders, bool useCachedResult)
        {
            var exclude = excludeFolders.Select(ef => ef.ToLower()).ToArray();
            return await GetAllWithCacheAsync<FileEntity>(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude)
                    .Where(fe => expectedDirectory.Any(ed => fe.Directory != null && ed.Equals(fe.Directory.Id))
                        && exclude.All(ef => !fe.LC_FilePhysicalFullPath.Contains(ef))),
                cacheKey: GetCacheKey<FileEntity>(expectedDirectory.Select(ed => ed.ToString())),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public async Task<IEnumerable<FileEntity>> GetAllByParentDirectoryIdsAsync(IEnumerable<string> expectedDirectory, IEnumerable<string> excludeFolders, bool useCachedResult)
        {
            return await GetAllByParentDirectoryIdsAsync(expectedDirectory.Select(ed => Guid.TryParse(ed, out var dbGuid) ? dbGuid : new Guid()), excludeFolders, useCachedResult);
        }
        public async Task<IEnumerable<string>> GetAllFileFullNamesAsync(bool useCachedResult)
        {
            return await GetAllWithCacheAsync<string>(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .AsNoTracking()
                    .Select(static (f) => f.FilePhysicalFullPath),
                cacheKey: GetCacheKey<string>(methodName: nameof(GetAllFileFullNamesAsync)),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public async Task<(bool ok, int? minDepth)> GetMinimalDepthAsync(bool useCachedResult)
        {
            var minDepth = await GetSingleWithCacheAsync<int>(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .AsNoTracking()
                    .Include(f => f.Directory)
                    .MinAsync(static (f) => f.Directory != null ? f.Directory.Depth : short.MaxValue),
                cacheKey: GetCacheKey<int>(methodName: nameof(GetMinimalDepthAsync)),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return minDepth == short.MaxValue ? (false, null) : (true, minDepth);
        }
        public async Task<IEnumerable<FileEntity>> GetAllByDirectoryDepthAsync(int depth, bool useCachedResult)
        {
            return await GetAllWithCacheAsync<FileEntity>(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .AsNoTracking()
                    .Include(f => f.Directory)
                    .Where(f => f.Directory != null && f.Directory.Depth == depth),
                cacheKey: GetCacheKey<FileEntity>([depth.ToString()]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public async Task<IEnumerable<FileEntity>> GetAllByDirectoryDepthAsync(int depth, int skip, int take, bool useCachedResult)
        {
            return await GetAllWithCacheAsync<FileEntity>(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .AsNoTracking()
                    .Include(f => f.Directory)
                    .Where(f => f.Directory != null && f.Directory.Depth == depth)
                    .Skip(skip)
                    .Take(take),
                cacheKey: GetCacheKey<FileEntity>([depth.ToString(), skip.ToString(), take.ToString()]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public async Task<IEnumerable<FileEntity>> GetAllByPathFullNameAsync(string pathFullName, bool useCachedResult)
        {
            pathFullName = pathFullName.ToLower();
            return await GetAllWithCacheAsync<FileEntity>(
                queryAction: DbSet
                    .OrderEntitiesByDefault(DefaultOrderBy)
                    .IncludeChildEntities(DefaultInclude)
                    .Where(f => f.LC_FilePhysicalFullPath.Equals(pathFullName)),
                cacheKey: GetCacheKey<FileEntity>([pathFullName]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
    }
}
