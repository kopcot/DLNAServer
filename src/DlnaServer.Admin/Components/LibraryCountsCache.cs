using DlnaServer.Core.Contracts;

namespace DlnaServer.Admin.Components
{
    /// <summary>
    /// The Dashboard's library counts, shared by every Dashboard in the process and kept until the
    /// library changes.
    /// </summary>
    /// <remarks>
    /// Counting reads every row of <c>Files</c> - about 70 ms on the NAS, and a disc that can never spin
    /// down while a Dashboard sits open in a tab, because the page re-counted every 30 seconds whether
    /// anything had changed or not. A snapshot is reused while <see cref="Core.Hosting.ILibraryChangeSignal"/>
    /// has not moved and the hidden folders are the same ones it was counted under; <see cref="MaxAge"/>
    /// is the safety net for a writer that forgets to mark.
    /// <para>
    /// Shared rather than per page because each page load builds the Dashboard twice - once pre-rendered,
    /// once when the circuit connects - and both used to count, as does every other open tab.
    /// </para>
    /// </remarks>
    internal sealed class LibraryCountsCache
    {
        internal static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

        private Snapshot? _current;

        /// <summary>
        /// The stored counts when they are still current for these conditions, otherwise null.
        /// </summary>
        internal Snapshot? Find(long generation, string hiddenFolders, DateTimeOffset now)
        {
            var current = Volatile.Read(ref _current);

            return current is not null
                && current.Generation == generation
                && string.Equals(current.HiddenFolders, hiddenFolders, StringComparison.Ordinal)
                && now - current.TakenAt < MaxAge
                    ? current
                    : null;
        }

        /// <remarks>
        /// The caller reads the generation <b>before</b> counting and stores it with the result, so a
        /// change landing mid-count leaves the snapshot one generation behind and the next look recounts.
        /// </remarks>
        internal void Store(Snapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            Volatile.Write(ref _current, snapshot);
        }

        /// <summary>
        /// One set of counts and the conditions they were taken under.
        /// </summary>
        internal sealed record Snapshot(
            long Generation,
            string HiddenFolders,
            DateTimeOffset TakenAt,
            LibraryCountsDto Counts,
            int DirectoryCount);
    }
}
