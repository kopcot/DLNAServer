using System.ComponentModel.DataAnnotations;

namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// SQLite tuning. Every value is optional; zero means "leave SQLite's own default alone".
    /// </summary>
    public sealed class DatabaseOptions
    {
        /// <summary>
        /// Size of the memory-mapped region, capped at the actual database file size. Zero disables mapping.
        /// </summary>
        [Range(0, 65_536, ErrorMessage = "Index kept in memory must be between 0 and 65536 MB.")]
        public int MemoryMapLimitInMegabytes { get; set; }

        /// <summary>
        /// Page-cache budget <b>per connection</b>. Zero leaves SQLite's default in place.
        /// </summary>
        /// <remarks>
        /// Per connection, not per process - <c>PRAGMA cache_size</c> is connection-scoped and is applied
        /// in <c>ConnectionOpened</c>, so the real cost is this value times the number of live pooled
        /// connections. A scoped <c>DbContext</c> per request plus seven hosted services plus Blazor
        /// circuits makes 15-30 ordinary, so 8 MB here was 120-240 MB of native memory that appears in
        /// the working set and nowhere in the managed heap. This value is therefore the <b>only</b>
        /// control over that total: <c>Microsoft.Data.Sqlite</c> has no <c>Max Pool Size</c> keyword
        /// (it throws <c>ArgumentException</c> on one) and exposes no other way to cap the pool, so the
        /// connection count cannot be bounded and the per-connection budget has to carry it alone.
        /// <para>
        /// The reference exposed this setting but never applied it: a missing <c>else</c> meant a
        /// hard-coded value always overwrote the configured one on every connection.
        /// </para>
        /// </remarks>
        [Range(0, 4096, ErrorMessage = "Lookup cache must be between 0 and 4096 MB per connection.")]
        public int CacheSizeInMegabytes { get; set; }

        /// <summary>
        /// Hold temporary tables and indices in memory rather than on disk.
        /// </summary>
        public bool UseMemoryTempStore { get; set; }

        /// <summary>
        /// Log any query slower than this. Zero disables slow-query logging.
        /// </summary>
        [Range(0, 60_000, ErrorMessage = "Reporting slow lookups must be between 0 and 60000 ms, or 0 to turn it off.")]
        public int SlowQueryThresholdInMilliseconds { get; set; } = 50;

        /// <summary>
        /// How long a statement waits for the write lock before giving up.
        /// </summary>
        /// <remarks>
        /// SQLite allows one writer at a time and this process has six background services plus request
        /// threads. Without an explicit timeout the wait was whatever the ADO.NET default happened to be,
        /// which is not something to leave to chance on the path where a scan and a renderer collide.
        /// </remarks>
        [Range(0, 120_000, ErrorMessage = "Waiting for the index must be between 0 and 120000 ms.")]
        public int BusyTimeoutInMilliseconds { get; set; } = 5_000;
    }
}
