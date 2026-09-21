namespace DlnaServer.Core.Files
{
    /// <summary>
    /// Picks the point in a video to grab a thumbnail from.
    /// </summary>
    public static class VideoCaptureTime
    {
        /// <summary>
        /// Returns an offset far enough in to skip titles and black opening frames, scaled to duration.
        /// </summary>
        /// <remarks>
        /// The tiers are carried over from the reference verbatim. It is a heuristic, not scene
        /// detection: grabbing frame zero of a film usually yields a black rectangle.
        /// </remarks>
        public static TimeSpan For(TimeSpan duration)
        {
            return duration switch
            {
                { TotalHours: > 1 } => TimeSpan.FromMinutes(30),
                { TotalMinutes: > 40 } => TimeSpan.FromMinutes(10),
                { TotalMinutes: > 20 } => TimeSpan.FromMinutes(5),
                { TotalMinutes: > 5 } => TimeSpan.FromMinutes(1),
                { TotalMinutes: > 2 } => TimeSpan.FromSeconds(30),
                { TotalSeconds: > 30 } => TimeSpan.FromSeconds(10),
                { TotalSeconds: > 5 } => TimeSpan.FromSeconds(2),
                _ => TimeSpan.Zero,
            };
        }
    }
}
