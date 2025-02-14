using DLNAServer.Database.Entities;

namespace DLNAServer.Helpers.Database
{
    public static class QueryExtensions
    {
        public static IQueryable<T> IncludeChildEntities<T>(this IQueryable<T> query, Func<IQueryable<T>, IQueryable<T>> queryIncludesChildEntities) where T : BaseEntity
        {
            ArgumentNullException.ThrowIfNull(query, nameof(query));
            ArgumentNullException.ThrowIfNull(queryIncludesChildEntities, nameof(queryIncludesChildEntities));

            return queryIncludesChildEntities(query);
        }
        public static IQueryable<T> OrderEntitiesByDefault<T>(this IQueryable<T> query, Func<IQueryable<T>, IOrderedQueryable<T>> DefaultOrderBy) where T : BaseEntity
        {
            ArgumentNullException.ThrowIfNull(query, nameof(query));
            ArgumentNullException.ThrowIfNull(DefaultOrderBy, nameof(DefaultOrderBy));

            return DefaultOrderBy(query);
        }
    }
}
