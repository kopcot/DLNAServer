namespace DlnaServer.Core.Diagnostics
{
    /// <summary>
    /// Whether the admin pages label each value they show with where it came from.
    /// </summary>
    /// <remarks>
    /// Off by default and switched on for a period rather than saved, for the same two reasons the
    /// temporary folder window is: it is wanted while something is being investigated and not afterwards,
    /// and a setting reachable from <c>DlnaOptions</c> is written to <c>config.json</c> on the next save
    /// whether or not anyone asked for it. A restart turns it off again.
    /// </remarks>
    public interface ITechnicalTooltips
    {
        /// <summary>
        /// True while values carry their source.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// When the current period lapses, or null when they are off.
        /// </summary>
        DateTimeOffset? EnabledUntilUtc { get; }

        /// <summary>
        /// Labels every value for the given period, replacing any period already running.
        /// </summary>
        void EnableFor(TimeSpan duration);

        /// <summary>
        /// Stops labelling them immediately.
        /// </summary>
        void Disable();
    }
}
