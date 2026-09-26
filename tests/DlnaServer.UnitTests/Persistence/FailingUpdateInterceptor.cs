using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DlnaServer.UnitTests.Persistence
{
    /// <summary>
    /// Fails every <c>UPDATE</c> before it reaches the database, standing in for a crash between the
    /// statements of a multi-statement write.
    /// </summary>
    internal sealed class FailingUpdateInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Simulated failure between the delete and the update.");
            }

            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
