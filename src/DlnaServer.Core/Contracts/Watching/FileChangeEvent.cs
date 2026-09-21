namespace DlnaServer.Core.Contracts.Watching
{
    /// <summary>
    /// One filesystem change observed under a watched source folder.
    /// </summary>
    /// <param name="FullPath">The path the event concerns.</param>
    /// <param name="OldFullPath">Previous path, for a rename. Null otherwise.</param>
    /// <param name="Kind">What happened.</param>
    /// <param name="OccurredTimestamp">
    /// When the event was raised, as a <see cref="TimeProvider.GetTimestamp"/> reading. The consumer
    /// waits until a path has been quiet for a while before acting, and measures that wait from here
    /// with <see cref="TimeProvider.GetElapsedTime(long)"/>.
    /// </param>
    /// <remarks>
    /// A monotonic timestamp rather than a wall-clock <see cref="DateTime"/>, which is what this
    /// carried. The settle window is an elapsed-time question, and the wall clock is not monotonic: a
    /// NAS without a battery-backed clock starts wrong and NTP steps it. A backward step stalled every
    /// pending path for the length of the step, and a forward step marked them all settled at once and
    /// indexed files mid-copy - precisely what <c>Library.FileSettleSeconds</c> exists to prevent.
    /// </remarks>
    public sealed record FileChangeEvent(
        string FullPath,
        string? OldFullPath,
        FileChangeKind Kind,
        long OccurredTimestamp);
}
