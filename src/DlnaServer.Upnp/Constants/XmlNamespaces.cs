namespace DlnaServer.Upnp.Constants
{
    /// <summary>
    /// XML namespaces used in UPnP and DIDL-Lite documents.
    /// </summary>
    /// <remarks>
    /// Values are verbatim from the reference. Renderers match these literally, so a change here is a
    /// change on the wire.
    /// </remarks>
    public static class XmlNamespaces
    {
        /// <summary>
        /// <b>xmlns:dc</b><br />
        /// Dublin Core, source of <c>dc:title</c> and <c>dc:date</c>.
        /// </summary>
        public const string DublinCore = "http://purl.org/dc/elements/1.1/";

        /// <summary>
        /// <b>xmlns:upnp</b><br />
        /// UPnP metadata, source of <c>upnp:class</c>, <c>upnp:albumArtURI</c> and the codec elements.
        /// </summary>
        public const string Upnp = "urn:schemas-upnp-org:metadata-1-0/upnp/";

        /// <summary>
        /// <b>xmlns:sec</b><br />
        /// Samsung vendor extensions, as used inside DIDL-Lite.
        /// </summary>
        /// <remarks>
        /// Deliberately different from the namespace of the same prefix in <c>description.xml</c>, which
        /// is <c>http://www.sec.co.kr/dlna</c>. The two documents genuinely use different URIs and both
        /// are reproduced as the reference emits them.
        /// </remarks>
        public const string Samsung = "http://www.sec.co.kr/";

        /// <summary>
        /// <b>xmlns:dlna</b><br />
        /// DLNA metadata extensions.
        /// </summary>
        public const string Dlna = "urn:schemas-dlna-org:metadata-1-0/";

        /// <summary>
        /// <b>xmlns</b><br />
        /// Default namespace of a DIDL-Lite document.
        /// </summary>
        public const string DidlLite = "urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/";
    }
}
