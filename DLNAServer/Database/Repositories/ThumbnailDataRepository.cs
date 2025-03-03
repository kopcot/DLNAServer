using DLNAServer.Common;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DLNAServer.Database.Repositories
{
    public class ThumbnailDataRepository : BaseRepository<ThumbnailDataEntity>, IThumbnailDataRepository
    {
        public ThumbnailDataRepository(DlnaDbContext dbContext, Lazy<IMemoryCache> memoryCacheLazy) : base(dbContext, memoryCacheLazy, nameof(ThumbnailDataRepository))
        {
            defaultCacheDuration = TimeSpanValues.Time5min;
            defaultCacheAbsoluteDuration = TimeSpanValues.Time1hour;
        }
    }
}
