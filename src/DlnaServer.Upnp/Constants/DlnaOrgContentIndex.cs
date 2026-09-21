namespace DlnaServer.Upnp.Constants
{
    /// <summary>
    /// The value of <c>DLNA.ORG_CI</c>, which says whether a resource is the media itself or a smaller
    /// stand-in for it.
    /// </summary>
    /// <remarks>
    /// Written on the wire as a decimal digit. It matters because a television offered two resources for
    /// one item has to know which is the picture and which is the film.
    /// </remarks>
    public enum DlnaOrgContentIndex : short
    {
        /// <summary>
        /// The resource is the media itself, at full size.
        /// </summary>
        NoSpecificIndex = 0,

        /// <summary>
        /// The resource is a thumbnail of the item rather than the item.
        /// </summary>
        Thumbnail = 1,
    }
}
