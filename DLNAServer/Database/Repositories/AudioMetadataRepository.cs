using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DLNAServer.Database.Repositories
{
    public class AudioMetadataRepository : BaseRepository<MediaAudioEntity>, IAudioMetadataRepository
    {
        public AudioMetadataRepository(DlnaDbContext dbContext, Lazy<IMemoryCache> memoryCacheLazy) : base(dbContext, memoryCacheLazy, nameof(AudioMetadataRepository))
        {
        }
    }
}
