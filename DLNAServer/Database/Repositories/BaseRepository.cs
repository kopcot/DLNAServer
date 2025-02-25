using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Helpers.Caching;
using DLNAServer.Helpers.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DLNAServer.Database.Repositories
{
    public abstract class BaseRepository<T> : IBaseRepository<T>, IDisposable where T : BaseEntity
    {
        protected readonly DlnaDbContext DbContext;
        protected DbSet<T> DbSet => DbContext.Set<T>();

        private readonly Lazy<IMemoryCache> _memoryCacheLazy;
        protected IMemoryCache MemoryCache => _memoryCacheLazy.Value;
        protected readonly string _repositoryName;
        protected BaseRepository(DlnaDbContext dbContext, Lazy<IMemoryCache> memoryCacheLazy, string repositoryName)
        {
            DbContext = dbContext;
            _memoryCacheLazy = memoryCacheLazy;
            _repositoryName = repositoryName;
        }
        protected TimeSpan defaultCacheDuration = TimeSpan.FromSeconds(5);
        protected TimeSpan defaultCacheAbsoluteDuration = TimeSpan.FromMinutes(5);
        protected virtual Func<IQueryable<T>, IOrderedQueryable<T>> DefaultOrderBy { get; set; } = static (query) => query.OrderByDescending(static (e) => e.CreatedInDB);
        protected virtual Func<IQueryable<T>, IQueryable<T>> DefaultInclude { get; set; } = static (query) => query;
        DlnaDbContext IBaseRepository<T>.DbContext => DbContext;

        public async Task<bool> SaveChangesAsync()
        {
            _ = await DbContext.SaveChangesAsync();
            return true;
        }
        public void MarkForDelete<T1>(T1 entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            _ = DbContext.Remove(entity);
        }
        public async Task<bool> DeleteAllAsync()
        {
            using (var transaction = DbContext.Database.BeginTransaction())
            {
                DbSet.RemoveRange(DbSet);
                _ = await DbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            };
            return true;
        }
        public async Task<bool> DeleteAsync(T entity)
        {
            _ = DbSet.Remove(entity);
            _ = await DbContext.SaveChangesAsync();
            return true;
        }
        public async Task<bool> DeleteByGuidAsync(string guid)
        {
            var entities = await GetByIdAsync(guid, false);
            if (entities != null)
            {
                return await DeleteAsync(entities);
            }

            return false;
        }
        public async Task<bool> DeleteRangeAsync(IEnumerable<T> entities)
        {
            using (var transaction = DbContext.Database.BeginTransaction())
            {
                DbSet.RemoveRange(entities);
                _ = await DbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            };
            return true;
        }
        public async Task<bool> DeleteRangeByGuidsAsync(IEnumerable<Guid> guids)
        {
            using (var transaction = DbContext.Database.BeginTransaction())
            {
                var entities = await GetAllByIdsAsync(guids, false);
                DbSet.RemoveRange(entities);
                _ = await DbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            };
            return true;
        }
        public async Task<bool> DeleteRangeByGuidsAsync(IEnumerable<string> guids)
        {
            List<Guid> guidsParsed = [];
            foreach (var guid in guids.ToList())
            {
                if (Guid.TryParse(guid, out var dbGuid))
                {
                    guidsParsed.Add(dbGuid);
                }
            }

            return await DeleteRangeByGuidsAsync(guidsParsed);
        }
        public async Task<T[]> GetAllAsync(bool useCachedResult) => await GetAllAsync(false, useCachedResult);
        public async Task<T[]> GetAllAsync(bool asNoTracking, bool useCachedResult)
        {
            var memoryDataResult = await GetAllWithCacheAsync(
                queryAction: asNoTracking
                        ? DbSet
                            .OrderEntitiesByDefault(DefaultOrderBy)
                            .IncludeChildEntities(DefaultInclude)
                            .AsNoTracking()
                        : DbSet
                            .OrderEntitiesByDefault(DefaultOrderBy)
                            .IncludeChildEntities(DefaultInclude),
                cacheKey: GetCacheKey<T>(),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ToArray();
        }

        public async Task<T[]> GetAllAsync(int skip, int take, bool useCachedResult) => await GetAllAsync(skip, take, false, useCachedResult);
        public async Task<T[]> GetAllAsync(int skip, int take, bool asNoTracking, bool useCachedResult)
        {
            var memoryDataResult = await GetAllWithCacheAsync(
                queryAction: asNoTracking
                        ? DbSet
                            .OrderEntitiesByDefault(DefaultOrderBy)
                            .IncludeChildEntities(DefaultInclude)
                            .AsNoTracking()
                            .Skip(skip)
                            .Take(take)
                        : DbSet
                            .OrderEntitiesByDefault(DefaultOrderBy)
                            .IncludeChildEntities(DefaultInclude)
                            .Skip(skip)
                            .Take(take),
                cacheKey: GetCacheKey<T>(additionalArgs: [asNoTracking.ToString(), skip.ToString(), take.ToString()]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ToArray();
        }
        public async Task<long> GetCountAsync(bool useCachedResult)
        {
            return await GetSingleWithCacheAsync(
                queryAction: DbSet.LongCountAsync(),
                cacheKey: GetCacheKey<T>(),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }

        public async Task<T[]> GetAllByIdsAsync(IEnumerable<Guid> guids, bool useCachedResult)
        {
            var memoryDataResult = await GetAllWithCacheAsync(
                queryAction: DbSet
                        .OrderEntitiesByDefault(DefaultOrderBy)
                        .IncludeChildEntities(DefaultInclude)
                        .Where(e => guids.Contains(e.Id)),
                cacheKey: GetCacheKey<T>(guids.Select(static (g) => g.ToString())),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ToArray();
        }
        public async Task<T?> GetByIdAsync(Guid guid, bool useCachedResult) => await GetByIdAsync(guid, false, useCachedResult);
        public async Task<T?> GetByIdAsync(Guid guid, bool asNoTracking, bool useCachedResult)
        {
            return await GetSingleWithCacheAsync(
                queryAction: asNoTracking
                        ? DbSet
                            .OrderEntitiesByDefault(DefaultOrderBy)
                            .IncludeChildEntities(DefaultInclude)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(e => e.Id == guid)
                        : DbSet
                            .OrderEntitiesByDefault(DefaultOrderBy)
                            .IncludeChildEntities(DefaultInclude)
                            .FirstOrDefaultAsync(e => e.Id == guid),
                cacheKey: GetCacheKey<T>([asNoTracking.ToString(), guid.ToString()]),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public async Task<T?> GetByIdAsync(string guid, bool useCachedResult)
        {
            return Guid.TryParse(guid, out var dbGuid) ? await GetByIdAsync(dbGuid, useCachedResult) : null;
        }
        public async Task<T?> GetByIdAsync(string guid, bool asNoTracking, bool useCachedResult)
        {
            return Guid.TryParse(guid, out var dbGuid) ? await GetByIdAsync(dbGuid, asNoTracking, useCachedResult) : null;
        }
        public async Task<bool> AddAsync(T entity)
        {
            T[] entities = [entity];
            return await AddRangeAsync(entities);
        }
        public async Task<bool> AddRangeAsync(IEnumerable<T> entities)
        {
            using (var transaction = DbContext.Database.BeginTransaction())
            {
                await DbSet.AddRangeAsync(entities);
                _ = await DbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            };
            return true;
        }
        public async Task<bool> UpdateAsync(T entity)
        {
            T[] entities = [entity];
            return await UpdateRangeAsync(entities);
        }
        public async Task<bool> UpdateRangeAsync(T[] entities)
        {
            using (var transaction = DbContext.Database.BeginTransaction())
            {
                DbSet.UpdateRange(entities);
                _ = await DbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            };
            return true;
        }
        public async Task<bool> UpsertAsync(T entity)
        {
            T[] entities = [entity];
            return await UpsertRangeAsync(entities);
        }
        public async Task<bool> UpsertRangeAsync(T[] entities)
        { 
            var existingById = (await GetAllByIdsAsync(entities.Select(static (e) => e.Id), false)).ToArray();
            var notExist = entities.Where(e => !existingById.Any(ex => ex.Id.Equals(e.Id))).ToArray();
            var exist = entities.Where(e => existingById.Any(ex => ex.Id.Equals(e.Id))).ToArray();

            using (var transaction = DbContext.Database.BeginTransaction())
            {
                if (exist.Length > 0)
                {
                    DbSet.UpdateRange(exist);
                }

                if (notExist.Length > 0)
                {
                    DbSet.AddRange(notExist);
                }

                _ = await DbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            };

            existingById = [];
            notExist = [];
            exist = [];

            return true; 
        }  
        public async Task<bool> IsAnyItemAsync()
        {
            return await DbSet
                .AsNoTracking()
                .AnyAsync();
        }

        #region Helpers 
        protected async Task<ReadOnlyMemory<TResult>> GetAllWithCacheAsync<TResult>(
            IQueryable<TResult> queryAction,
            string cacheKey,
            TimeSpan cacheDuration,
            bool useCachedResult)
        {
            if (useCachedResult)
            {
                var resultAsMemory = await MemoryCache.GetOrCreateAsync(
                    cacheKey,
                    async entry =>
                    {
                        var data = await queryAction.ToArrayAsync();

                        entry.Value = data.AsReadOnly();
                        entry.SlidingExpiration = cacheDuration;
                        entry.AbsoluteExpirationRelativeToNow = defaultCacheAbsoluteDuration;
                        entry.Size = 1; // size is not important for entities vs physical cached file size

                        return data;
                    });

                MemoryCache.StartEvictCachedKey(cacheKey, cacheDuration);

                return resultAsMemory;
            }
            else
            {
                MemoryCache.Remove(cacheKey);

                return await queryAction.ToArrayAsync();
            }
        }
        protected async Task<TResult?> GetSingleWithCacheAsync<TResult>(
            Task<TResult> queryAction,
            string cacheKey,
            TimeSpan cacheDuration,
            bool useCachedResult)
        {
            if (useCachedResult)
            {
                var result = await MemoryCache.GetOrCreateAsync(
                    cacheKey,
                    async entry =>
                    {
                        var resultSingle = (await queryAction);

                        entry.Value = resultSingle;
                        entry.SlidingExpiration = cacheDuration;
                        entry.AbsoluteExpirationRelativeToNow = defaultCacheAbsoluteDuration;
                        entry.Size = 1; // size is not important for entities vs physical cached file size

                        return resultSingle;
                    });

                MemoryCache.StartEvictCachedKey(cacheKey, cacheDuration);

                return result;
            }
            else
            {
                MemoryCache.Remove(cacheKey);

                return await queryAction;
            }
        }

        protected string GetCacheKey<TResult>(IEnumerable<string>? additionalArgs = null, [System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
        {
            if (additionalArgs == null || !additionalArgs.Any())
            {
                return $"{_repositoryName} {methodName} {typeof(TResult).Name}";
            }
            return $"{_repositoryName} {methodName} {typeof(TResult).Name} {string.Join(';', additionalArgs)}";
        }
        #endregion

        #region Dispose
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~BaseRepository()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
