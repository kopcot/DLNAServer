using DLNAServer.Database.Entities;

namespace DLNAServer.Database.Repositories.Interfaces
{
    public interface IThumbnailRepository : IBaseRepository<ThumbnailEntity>
    {
        Task<ThumbnailEntity[]> GetAllByPathFullNameAsync(string pathFullName, bool useCachedResult = true);
    }
}
