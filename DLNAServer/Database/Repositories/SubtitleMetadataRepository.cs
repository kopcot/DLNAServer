using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DLNAServer.Database.Repositories
{
    public class SubtitleMetadataRepository : BaseRepository<MediaSubtitleEntity>, ISubtitleMetadataRepository
    {
        public SubtitleMetadataRepository(DlnaDbContext dbContext, Lazy<IMemoryCache> memoryCacheLazy) : base(dbContext, memoryCacheLazy, nameof(SubtitleMetadataRepository))
        {
        }
    }
}
