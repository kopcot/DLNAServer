namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// A <see cref="TimeProvider"/> whose clock only moves when a test moves it, so expiry can be
    /// asserted without waiting for it.
    /// </summary>
    /// <remarks>
    /// Hand-written rather than taken from <c>Microsoft.Extensions.TimeProvider.Testing</c>, which is not
    /// otherwise used in this solution.
    /// <para>
    /// Drives the monotonic clock as well as the wall clock, and both from the same
    /// <see cref="Advance"/>. The settle and coalesce windows moved to
    /// <see cref="TimeProvider.GetTimestamp"/> so an NTP step could not decide whether a file had
    /// finished copying - and overriding only <c>GetUtcNow</c> would have left exactly those windows
    /// undrivable, which is the one thing this type exists for.
    /// </para>
    /// <para>
    /// <see cref="TimestampFrequency"/> is ticks per second, so a timestamp is simply the tick count of
    /// the wall clock and <see cref="TimeProvider.GetElapsedTime(long)"/> returns precisely what
    /// <see cref="Advance"/> was given.
    /// </para>
    /// <para>
    /// It deliberately does NOT override <c>CreateTimer</c>, so <c>Task.Delay(_, timeProvider, _)</c>
    /// still waits on the real clock. A loop paced by that cannot be fast-forwarded with this; test the
    /// steps it calls instead, which is why <c>FileWatcherHostedService</c> exposes them.
    /// </para>
    /// </remarks>
    internal sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public override long GetTimestamp()
        {
            return _utcNow.UtcTicks;
        }

        public void Advance(TimeSpan amount)
        {
            _utcNow = _utcNow.Add(amount);
        }
    }
}
