using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DlnaServer.Persistence
{
    /// <summary>
    /// Builds a context for <c>dotnet ef</c> at design time.
    /// </summary>
    /// <remarks>
    /// The connection string here is only ever used to generate migration code, never to reach a real
    /// database - migrations are applied at runtime against the configured connection.
    /// </remarks>
    internal sealed class DesignTimeDlnaDbContextFactory : IDesignTimeDbContextFactory<DlnaDbContext>
    {
        public DlnaDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<DlnaDbContext>()
                .UseSqlite("Data Source=design-time.sqlite")
                .Options;

            return new DlnaDbContext(options);
        }
    }
}
