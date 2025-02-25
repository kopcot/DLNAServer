namespace DLNAServer.Database.Repositories.Interfaces
{
    public interface IBaseRepository<T>
    {
        DlnaDbContext DbContext { get; }
        Task<bool> AddAsync(T entity); 
        Task<bool> AddRangeAsync(IEnumerable<T> entities);
        Task<T?> GetByIdAsync(Guid guid, bool useCachedResult = true);
        Task<T?> GetByIdAsync(Guid guid, bool asNoTracking = false, bool useCachedResult = true);
        Task<T?> GetByIdAsync(string guid, bool useCachedResult = true);
        Task<T?> GetByIdAsync(string guid, bool asNoTracking = false, bool useCachedResult = true);
        Task<T[]> GetAllAsync(bool useCachedResult = true);
        Task<T[]> GetAllAsync(bool asNoTracking = false, bool useCachedResult = true);
        Task<T[]> GetAllAsync(int skip, int take, bool useCachedResult = true);
        Task<T[]> GetAllAsync(int skip, int take, bool asNoTracking = false, bool useCachedResult = true);
        Task<T[]> GetAllByIdsAsync(IEnumerable<Guid> guids, bool useCachedResult = true);
        Task<long> GetCountAsync(bool useCachedResult = true);
        Task<bool> DeleteAllAsync(); 
        Task<bool> DeleteRangeAsync(IEnumerable<T> entities);
        Task<bool> DeleteByGuidAsync(string guid);
        Task<bool> DeleteAsync(T entity);
        Task<bool> DeleteRangeByGuidsAsync(IEnumerable<Guid> guids);
        Task<bool> DeleteRangeByGuidsAsync(IEnumerable<string> guids);
        Task<bool> IsAnyItemAsync();
        Task<bool> SaveChangesAsync();
        void MarkForDelete<T1>(T1 entity);

        /// <summary>
        /// Use with care as DbSet.Update / DbSet.UpdateRange / ... change state of entity as Modified and his child entities
        /// that mean, <br />EF-Core will treat them as needing to be updated in database
        /// <br /><br />
        /// <b>IMPORTANT:</b> setting child-entity to null in the parent-entity property will not trigger automatically
        /// delete action for child-entity<br />
        /// - EF Core does not detect "null" as a deletion when using Update/UpdateRange. <br />
        /// - You must explicitly mark the child entity for deletion using context.Remove(childEntity)
        ///   or by setting its EntityState to Deleted. <br />
        /// <br />
        /// It is needed to mark them for deletion or to delete them in another command<br />
        /// If not handled, the-child entity may remain orphaned in the database
        /// </summary> 
        Task<bool> UpdateAsync(T entity);
        /// <inheritdoc cref="UpdateAsync(T)"/>
        Task<bool> UpdateRangeAsync(T[] entities);
        /// <inheritdoc cref="UpdateAsync(T)"/>
        Task<bool> UpsertAsync(T entity);
        /// <inheritdoc cref="UpdateAsync(T)"/>
        Task<bool> UpsertRangeAsync(T[] entities);
    }
}
