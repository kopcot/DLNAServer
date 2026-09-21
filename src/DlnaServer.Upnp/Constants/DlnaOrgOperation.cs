namespace DlnaServer.Upnp.Constants
{
    /// <summary>
    /// The bits of <c>DLNA.ORG_OP</c>, which tells a renderer how it may seek within a resource.
    /// </summary>
    /// <remarks>
    /// Written on the wire as two binary digits, byte-seek first then time-seek - so
    /// <see cref="TimeSeekSupported"/> alone is <c>01</c>. See
    /// <see cref="DlnaProtocolInfo.Format(DlnaOrgOperation)"/>.
    /// </remarks>
    [Flags]
    public enum DlnaOrgOperation : short
    {
        /// <summary>
        /// The resource cannot be seeked at all; a renderer must play it from the beginning.
        /// </summary>
        None = 0,

        /// <summary>
        /// The renderer may ask to start at a given time position.
        /// </summary>
        TimeSeekSupported = 1 << 0,

        /// <summary>
        /// The renderer may ask for a byte range.
        /// </summary>
        ByteSeekSupported = 1 << 1,
    }
}
