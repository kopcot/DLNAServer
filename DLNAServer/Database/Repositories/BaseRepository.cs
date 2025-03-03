using CommunityToolkit.HighPerformance;
using DLNAServer.Common;
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
        protected TimeSpan defaultCacheDuration = TimeSpanValues.Time5sec;
        protected TimeSpan defaultCacheAbsoluteDuration = TimeSpanValues.Time5min;
        protected virtual Func<IQueryable<T>, IOrderedQueryable<T>> DefaultOrderBy { get; set; } = static (query) => query.OrderByDescending(static (e) => e.CreatedInDB);
        protected virtual Func<IQueryable<T>, IQueryable<T>> DefaultInclude { get; set; } = static (query) => query;
        DlnaDbContext IBaseRepository<T>.DbContext => DbContext;

        public Task<int> SaveChangesAsync()
        {
            return DbContext.SaveChangesAsync();
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
            }
            return true;
        }
        public Task<bool> DeleteAsync(T entity)
        {
            _ = DbSet.Remove(entity);
            return DbContext.SaveChangesAsync().ContinueWith(static (e) => e.Result == 1);
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
            }
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
            }
            return true;
        }
        public Task<bool> DeleteRangeByGuidsAsync(IEnumerable<string> guids)
        {
            List<Guid> guidsParsed = [];
            foreach (var guid in guids.ToList())
            {
                if (Guid.TryParse(guid, out var dbGuid))
                {
                    guidsParsed.Add(dbGuid);
                }
            }
            return DeleteRangeByGuidsAsync(guidsParsed);
        }
        public Task<T[]> GetAllAsync(bool useCachedResult) => GetAllAsync(false, useCachedResult);
        public Task<T[]> GetAllAsync(bool asNoTracking, bool useCachedResult)
        {
            var memoryDataResult = GetAllWithCacheAsync(
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
            return memoryDataResult.ContinueWith(static (e) => e.Result.ToArray());
        }

        public Task<T[]> GetAllAsync(int skip, int take, bool useCachedResult) => GetAllAsync(skip, take, false, useCachedResult);
        public Task<T[]> GetAllAsync(int skip, int take, bool asNoTracking, bool useCachedResult)
        {
            var memoryDataResult = GetAllWithCacheAsync(
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
            return memoryDataResult.ContinueWith(static (e) => e.Result.ToArray());
        }
        public Task<long> GetCountAsync(bool useCachedResult)
        {
            return GetSingleWithCacheAsync(
                queryAction: DbSet.LongCountAsync(),
                cacheKey: GetCacheKey<T>(),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
        }
        public Task<T[]> GetAllByIdsAsync(IEnumerable<Guid> guids, bool useCachedResult)
        {
            var memoryDataResult = GetAllWithCacheAsync(
                queryAction: DbSet
                        .OrderEntitiesByDefault(DefaultOrderBy)
                        .IncludeChildEntities(DefaultInclude)
                        .Where(e => guids.Contains(e.Id)),
                cacheKey: GetCacheKey<T>(guids.Select(static (g) => g.ToString())),
                cacheDuration: defaultCacheDuration,
                useCachedResult: useCachedResult
                );
            return memoryDataResult.ContinueWith(static (e) => e.Result.ToArray());
        }
        public Task<T?> GetByIdAsync(Guid guid, bool useCachedResult) => GetByIdAsync(guid, false, useCachedResult);
        public Task<T?> GetByIdAsync(Guid guid, bool asNoTracking, bool useCachedResult)
        {
            return GetSingleWithCacheAsync(
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
        public Task<T?> GetByIdAsync(string guid, bool useCachedResult)
        {
            return Guid.TryParse(guid, out var dbGuid) ? GetByIdAsync(dbGuid, useCachedResult) : Task.FromResult<T?>(null);
        }
        public Task<T?> GetByIdAsync(string guid, bool asNoTracking, bool useCachedResult)
        {
            return Guid.TryParse(guid, out var dbGuid) ? GetByIdAsync(dbGuid, asNoTracking, useCachedResult) : Task.FromResult<T?>(null);
        }
        public Task<bool> AddAsync(T entity)
        {
            T[] entities = [entity];
            return AddRangeAsync(entities);
        }
        public async Task<bool> AddRangeAsync(IEnumerable<T> entities)
        {
            using (var transaction = DbContext.Database.BeginTransaction())
            {
                await DbSet.AddRangeAsync(entities);
                _ = await DbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            return true;
        }
        public Task<bool> UpdateAsync(T entity)
        {
            T[] entities = [entity];
            return UpdateRangeAsync(entities);
        }
        public async Task<bool> UpdateRangeAsync(T[] entities)
        {
            using (var transaction = DbContext.Database.BeginTransaction())
            {
                DbSet.UpdateRange(entities);
                _ = await DbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            return true;
        }
        public Task<bool> UpsertAsync(T entity)
        {
            T[] entities = [entity];
            return UpsertRangeAsync(entities);
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
            }

            existingById = [];
            notExist = [];
            exist = [];

            return true;
        }
        public Task<bool> IsAnyItemAsync()
        {
            return DbSet
                .AsNoTracking()
                .AnyAsync();
        }

        #region Helpers 
        protected Task<Memory<TResult>> GetAllWithCacheAsync<TResult>(
            IQueryable<TResult> queryAction,
            string cacheKey,
            TimeSpan cacheDuration,
            bool useCachedResult)
        {
            if (useCachedResult)
            {
                var resultAsMemory = MemoryCache.GetOrCreateAsync(
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

                return resultAsMemory.ContinueWith(static (t) => t.Result.AsMemory());
            }
            else
            {
                MemoryCache.Remove(cacheKey);

                return queryAction.ToArrayAsync().ContinueWith(static (t) => t.Result.AsMemory());
            }
        }
        protected Task<TResult?> GetSingleWithCacheAsync<TResult>(
            Task<TResult> queryAction,
            string cacheKey,
            TimeSpan cacheDuration,
            bool useCachedResult)
        {
            if (useCachedResult)
            {
                var result = MemoryCache.GetOrCreateAsync(
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

                return queryAction.ContinueWith(static (t) => (TResult?)t.Result);
            }
        }

        protected string GetCacheKey<TResult>(IEnumerable<string>? additionalArgs = null, [System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
        {
            if (additionalArgs == null || !additionalArgs.Any())
            {
                return string.Format("{0} {1} {2}", [_repositoryName, methodName, typeof(TResult).Name]);
            }
            return string.Format("{0} {1} {2} {3}", [_repositoryName, methodName, typeof(TResult).Name, string.Join(';', additionalArgs)]);
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
