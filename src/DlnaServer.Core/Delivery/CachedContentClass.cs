namespace DlnaServer.Core.Delivery
{
    /// <summary>
    /// The kind of content a cached payload is, which decides how long it is retained and what is
    /// evicted first when the budget is reached.
    /// </summary>
    /// <remarks>
    /// One lifetime for everything is wrong in both directions: a value chosen for a two-gigabyte film
    /// throws away a three-kilobyte icon far too often, and a value chosen for the icon holds the film
    /// long past the point the budget can afford it.
    /// </remarks>
    public enum CachedContentClass
    {
        /// <summary>
        /// A media file served to a renderer. The large class - a handful of these fills the budget.
        /// </summary>
        Media = 1,

        /// <summary>
        /// A generated preview image. Small, and re-requested every time a renderer redraws a folder.
        /// </summary>
        Thumbnail = 2,

        /// <summary>
        /// An icon or SCPD document from <c>Resources</c>. A fixed, tiny set referenced by nearly every
        /// response, so it is retained longest and evicted last.
        /// </summary>
        StaticAsset = 3,
    }
}
