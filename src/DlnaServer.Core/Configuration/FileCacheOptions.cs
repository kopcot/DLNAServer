using System.ComponentModel.DataAnnotations;

namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// In-memory caching of served bytes.
    /// </summary>
    /// <remarks>
    /// The point of this cache is acoustic, not throughput. The NAS uses mechanical drives that are
    /// audible in the room, so a file already served should not wake a spun-down disc to be read again.
    /// <para>
    /// Content falls into three classes with very different sizes and access patterns, so each gets its
    /// own lifetime rather than one shared setting - a value that suits a two-gigabyte film is wrong for
    /// a three-kilobyte icon, and vice versa.
    /// </para>
    /// </remarks>
    public sealed class FileCacheOptions
    {
        /// <summary>
        /// Sliding lifetime for the static assets class - icons, SCPD documents and anything else served
        /// from <c>Resources</c>.
        /// </summary>
        /// <remarks>
        /// Deliberately not configurable. These files are a fixed part of the application, total a few
        /// hundred kilobytes, and are referenced by nearly every response, so there is no deployment
        /// where tuning them is the right answer.
        /// </remarks>
        public static TimeSpan StaticAssetSlidingExpiration { get; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Absolute ceiling for a static asset, however often it is touched.
        /// </summary>
        public static TimeSpan StaticAssetAbsoluteExpiration { get; } = TimeSpan.FromDays(1);

        /// <summary>
        /// Absolute ceiling for a cached media file, however often it is touched.
        /// </summary>
        /// <remarks>
        /// An absolute cap outranks the sliding refresh, so at one hour a film longer than that was
        /// dropped and re-read from the disc <b>while it was being watched</b> - the exact noise this
        /// cache exists to prevent. Six hours is comfortably longer than any single playback, so the
        /// sliding window decides eviction in practice, which is the intent. The reference uses twelve.
        /// </remarks>
        public static TimeSpan MediaAbsoluteExpiration { get; } = TimeSpan.FromHours(6);

        /// <summary>
        /// Absolute ceiling for a cached thumbnail, however often it is touched.
        /// </summary>
        public static TimeSpan ThumbnailAbsoluteExpiration { get; } = TimeSpan.FromHours(12);

        /// <summary>
        /// Share of the machine's memory the cache may occupy, whatever
        /// <see cref="MaxTotalSizeInMegabytes"/> asks for.
        /// </summary>
        /// <remarks>
        /// Half of what <c>GC.GetGCMemoryInfo().TotalAvailableMemoryBytes</c> reports - which is NOT the
        /// machine's memory. That figure is already the runtime's own ceiling, so with
        /// <c>System.GC.HeapHardLimitPercent</c> at 25 it reads a quarter of the box, and halving it
        /// again compounds rather than protects. The earlier comment here claimed a 2 GB machine
        /// resolves to 1 GB; it resolves to 256 MB. It also never binds where the measurements were
        /// taken: on the 40 GB test NAS it lands at 5 GB, well above any configured value.
        /// <para>
        /// So this is a backstop against a nonsensical configured value, not the control. The control is
        /// <see cref="MaxTotalSizeInMegabytes"/>. Measure a constrained run before changing either: see
        /// the emulated-limit note in <c>docs/decisions.md</c> section 3b.
        /// </para>
        /// </remarks>
        public const int AvailableMemoryDivisor = 2;

        /// <summary>
        /// Share of the resolved total budget any single file may occupy.
        /// </summary>
        /// <remarks>
        /// Half. It used to read "matching a 512 MB file against the 1 GB default budget", which was true
        /// of the values this file shipped before they were corrected to 32 and 256 - and a stale number
        /// in a remark is not harmless: a reviewer read it during a whole-tree audit and reported the
        /// budget reverting to 1 GB on a failed configuration reload, which it does not. The clamp exists
        /// so that raising
        /// <see cref="MaxFileSizeInMegabytes"/> past the budget cannot make the cache hold one payload and
        /// nothing else, and so an oversized file is refused before it is read rather than read and then
        /// discarded by the store.
        /// </remarks>
        public const int MaxFileShareOfBudgetDivisor = 2;

        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Whether a Browse reply queues the previews it just listed into the cache.
        /// </summary>
        /// <remarks>
        /// On by default, because Browse is the only moment the server knows which thirty images a
        /// television is about to ask for. Switching it off leaves each preview to be read when it is
        /// actually requested: the folder still draws, one disc read at a time.
        /// <para>
        /// Separate from <see cref="Enabled"/> on purpose. That one decides whether anything is held in
        /// memory at all; this one decides only whether the cache is filled ahead of the request, which
        /// is the half that spends disc reads on images a renderer may never display - a television that
        /// pages straight through a folder pays for every preview in it.
        /// </para>
        /// </remarks>
        public bool WarmPreviewsOnBrowse { get; set; } = true;

        /// <summary>
        /// Total budget for cached bytes, clamped at half of the machine's memory
        /// (<see cref="AvailableMemoryDivisor"/>) at startup.
        /// </summary>
        /// <remarks>
        /// Matches what the shipped <c>config.json</c> asks for, which is the whole point of the value.
        /// It was 1,024 - four times the shipped file - so a deployment that arrived without a
        /// <c>config.json</c>, which is every fresh one, silently ran the configuration measured at
        /// 5,073 MB of working set against a 200 MB target. A code default that is more generous than
        /// the file it is meant to back up is not a default, it is a second configuration nobody reads.
        /// </remarks>
        [Range(0, 65_536)]
        public int MaxTotalSizeInMegabytes { get; set; } = 256;

        /// <summary>
        /// Files larger than this are always streamed from the disc and never cached.
        /// </summary>
        /// <remarks>
        /// A cached payload is one contiguous allocation, so this is also the largest single object the
        /// cache can put on the large object heap - a 512 MB film is one 512 MB array. That churn is what
        /// took the working set to 5073 MB on 2026-09-02, measured on a box reporting 40 GB free where
        /// the GC had no incentive to compact. Now 32 MB, matching the shipped <c>config.json</c>: the
        /// code default was 512, so a deployment without a configuration file ran the measured
        /// configuration rather than the shipped one. The total budget plus
        /// <see cref="MaxFileShareOfBudgetDivisor"/> bound how many can be held at once.
        /// <para>
        /// Also clamped to half of the resolved total budget, so one file can never occupy the cache on
        /// its own.
        /// </para>
        /// </remarks>
        [Range(0, 65_536)]
        public int MaxFileSizeInMegabytes { get; set; } = 32;

        /// <summary>
        /// Sliding lifetime for a cached media file, refreshed on each access.
        /// </summary>
        /// <remarks>
        /// Short by comparison with the other classes because these are the large items: a handful of
        /// films would exhaust the budget on their own. Long enough to cover playback of one item, since
        /// the window refreshes while the file is being read.
        /// </remarks>
        [Range(1, 1440)]
        public int MediaSlidingExpirationInMinutes { get; set; } = 10;

        /// <summary>
        /// Sliding lifetime for a cached thumbnail, refreshed on each access.
        /// </summary>
        /// <remarks>
        /// Longer than media because a thumbnail is small and is re-requested every time a renderer
        /// redraws the folder it belongs to. Kept well below the static-asset lifetime because a large
        /// library has one per file, so this class grows with the library while the static one does not.
        /// </remarks>
        [Range(1, 1440)]
        public int ThumbnailSlidingExpirationInMinutes { get; set; } = 60;
    }
}
