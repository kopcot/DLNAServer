namespace DlnaServer.UnitTests
{
    /// <summary>
    /// A <see cref="TimeProvider"/> whose clock only moves when a test moves it, so expiry can be
    /// asserted without waiting for it.
    /// </summary>
    /// <remarks>
    /// The integration tests have a fuller one of the same name - it drives the monotonic clock too,
    /// which the file watcher needs. This one covers the wall clock only, which is all the expiring state
    /// in Core reads. The two do not share a project, so they cannot share a file.
    /// </remarks>
    internal sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan amount)
        {
            _utcNow = _utcNow.Add(amount);
        }
    }
}
