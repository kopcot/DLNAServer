﻿using DLNAServer.Database.Entities;

namespace DLNAServer.Database.Repositories.Interfaces
{
    public interface IFileRepository : IBaseRepository<FileEntity>
    {
        Task<FileEntity[]> GetAllByAddedToDbAsync(int takeNumber, IEnumerable<string> excludeFolders, bool useCachedResult = true);
        Task<FileEntity[]> GetAllByParentDirectoryIdsAsync(IEnumerable<Guid> expectedDirectory, IEnumerable<string> excludeFolders, bool useCachedResult = true);
        Task<FileEntity[]> GetAllByParentDirectoryIdsAsync(IEnumerable<string> expectedDirectory, IEnumerable<string> excludeFolders, bool useCachedResult = true);
        Task<IEnumerable<string>> GetAllFileFullNamesAsync(bool useCachedResult = true);
        Task<(bool ok, int? minDepth)> GetMinimalDepthAsync(bool useCachedResult = true);
        Task<FileEntity[]> GetAllByDirectoryDepthAsync(int depth, bool useCachedResult = true);
        Task<FileEntity[]> GetAllByDirectoryDepthAsync(int depth, int skip, int take, bool useCachedResult = true);
        Task<FileEntity[]> GetAllByPathFullNameAsync(string pathFullName, bool useCachedResult = true);
    }
}
