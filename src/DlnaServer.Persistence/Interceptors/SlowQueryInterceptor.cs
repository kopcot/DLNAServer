using System.Data.Common;
using System.Globalization;
using DlnaServer.Core.Configuration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace DlnaServer.Persistence.Interceptors
{
    /// <summary>
    /// Logs database commands that take longer than the configured threshold.
    /// </summary>
    /// <remarks>
    /// The reference's equivalent wrote to a dedicated monthly log and proved its worth: over a month of
    /// production it recorded about 106 slow queries, the slowest at 101 ms, and that single entry
    /// identified the Browse query as the thing to fix. Without it there is no evidence for which query
    /// to optimise.
    /// <para>
    /// Parameter values are deliberately NOT logged. The reference enabled sensitive data logging and
    /// dumped every parameter, which for this server means full filesystem paths in a log file.
    /// </para>
    /// </remarks>
    internal sealed partial class SlowQueryInterceptor : DbCommandInterceptor
    {
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly ILogger<SlowQueryInterceptor> _logger;

        public SlowQueryInterceptor(IOptionsMonitor<DlnaOptions> options, ILogger<SlowQueryInterceptor> logger)
        {
            _options = options;
            _logger = logger;
        }

        public override DbDataReader ReaderExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result)
        {
            ArgumentNullException.ThrowIfNull(eventData);

            Report(eventData);

            return base.ReaderExecuted(command, eventData, result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventData);

            Report(eventData);

            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override int NonQueryExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result)
        {
            ArgumentNullException.ThrowIfNull(eventData);

            Report(eventData);

            return base.NonQueryExecuted(command, eventData, result);
        }

        private void Report(CommandExecutedEventData eventData)
        {
            var thresholdMilliseconds = _options.CurrentValue.Database.SlowQueryThresholdInMilliseconds;

            if (thresholdMilliseconds <= 0)
            {
                return;
            }

            var elapsed = eventData.Duration.TotalMilliseconds;

            if (elapsed < thresholdMilliseconds)
            {
                return;
            }

            LogSlowQuery(
                elapsed,
                thresholdMilliseconds,
                Summarise(eventData.Command.CommandText));
        }

        /// <summary>
        /// Collapses the command to a single line so one slow query is one log entry.
        /// </summary>
        private static string Summarise(string commandText)
        {
            const int maximumLength = 400;

            var singleLine = commandText
                .ReplaceLineEndings(" ")
                .Trim();

            return singleLine.Length <= maximumLength
                ? singleLine
                : string.Concat(singleLine.AsSpan(0, maximumLength), "...");
        }
    }
}
