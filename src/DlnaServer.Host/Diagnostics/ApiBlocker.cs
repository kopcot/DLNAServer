using DlnaServer.Core.Diagnostics;

namespace DlnaServer.Host.Diagnostics
{
    /// <inheritdoc cref="IApiBlocker"/>
    internal sealed class ApiBlocker : IApiBlocker
    {
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// The block, as a single reference so a reader never sees an expiry from one block beside the
        /// reason from another.
        /// </summary>
        private volatile ActiveBlock? _block;

        public ApiBlocker(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public bool IsBlocked => Current() is not null;

        public DateTimeOffset? BlockedUntilUtc => Current()?.UntilUtc;

        public string? Reason => Current()?.Reason;

        public void Block(TimeSpan duration, string reason)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration.Ticks);
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            _block = new ActiveBlock(_timeProvider.GetUtcNow().Add(duration), reason);
        }

        public void Release()
        {
            _block = null;
        }

        /// <summary>
        /// The block if it is still in force, clearing it once it has lapsed.
        /// </summary>
        private ActiveBlock? Current()
        {
            var block = _block;

            if (block is null)
            {
                return null;
            }

            if (_timeProvider.GetUtcNow() < block.UntilUtc)
            {
                return block;
            }

            // Lapsed. Cleared here rather than by a timer, so nothing has to be scheduled or disposed -
            // the block simply stops being true the next time anything asks.
            _block = null;

            return null;
        }

        private sealed record ActiveBlock(DateTimeOffset UntilUtc, string Reason);
    }
}
