using DLNAServer.Database.Entities;

namespace DLNAServer.Database.Repositories.Interfaces
{
    public interface IServerRepository : IBaseRepository<ServerEntity>
    {
        Task<string?> GetLastAccessMachineNameAsync();
    }
}