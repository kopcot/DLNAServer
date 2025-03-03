using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Helpers.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DLNAServer.Database.Repositories
{
    public class ServerRepository : BaseRepository<ServerEntity>, IServerRepository
    {
        public ServerRepository(DlnaDbContext dbContext, Lazy<IMemoryCache> memoryCacheLazy) : base(dbContext, memoryCacheLazy, nameof(ServerRepository))
        {
            DefaultOrderBy = static (entities) => entities
                .OrderByDescending(static (f) => f.LasAccess);
        }
        public Task<string?> GetLastAccessMachineNameAsync()
        {
            var lastAccess = DbSet
                .OrderEntitiesByDefault(DefaultOrderBy)
                .FirstOrDefaultAsync();
            return lastAccess.ContinueWith(static (se) => se.Result?.MachineName);
        }
    }
}
