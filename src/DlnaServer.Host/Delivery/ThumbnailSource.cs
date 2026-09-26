namespace DlnaServer.Host.Delivery
{
    /// <summary>
    /// Where a thumbnail response's bytes come from, as <see cref="IMediaContentResolver.ResolveThumbnailAsync"/>
    /// found them.
    /// </summary>
    public enum ThumbnailSource
    {
        /// <summary>
        /// Found nowhere: not in memory, not in the database and not on disc.
        /// </summary>
        None = 0,

        /// <summary>
        /// Served from the served-bytes cache, including a payload the resolver has just read into it.
        /// </summary>
        Cache = 1,

        /// <summary>
        /// Served from the copy stored in the database, which has now been put in the cache as well.
        /// </summary>
        Database = 2,

        /// <summary>
        /// The file exists but could not be held in memory, so it is streamed from disc as it is.
        /// </summary>
        Disc = 3,
    }
}
