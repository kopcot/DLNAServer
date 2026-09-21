using DlnaServer.Core.Diagnostics;

namespace DlnaServer.Host.Diagnostics
{
    /// <inheritdoc cref="ITechnicalTooltips"/>
    internal sealed class TechnicalTooltips : ITechnicalTooltips
    {
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// The period, as a single reference so a reader never sees an expiry that has already been
        /// replaced.
        /// </summary>
        private volatile OpenPeriod? _period;

        public TechnicalTooltips(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public bool IsEnabled => Current() is not null;

        public DateTimeOffset? EnabledUntilUtc => Current()?.UntilUtc;

        public void EnableFor(TimeSpan duration)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration.Ticks);

            _period = new OpenPeriod(_timeProvider.GetUtcNow().Add(duration));
        }

        public void Disable()
        {
            _period = null;
        }

        /// <summary>
        /// The period if it is still running, clearing it once it has lapsed.
        /// </summary>
        private OpenPeriod? Current()
        {
            var period = _period;

            if (period is null)
            {
                return null;
            }

            if (_timeProvider.GetUtcNow() < period.UntilUtc)
            {
                return period;
            }

            // Lapsed. Cleared here rather than by a timer, so nothing has to be scheduled or disposed -
            // the labels simply stop appearing the next time a page renders.
            _period = null;

            return null;
        }

        private sealed record OpenPeriod(DateTimeOffset UntilUtc);
    }
}
