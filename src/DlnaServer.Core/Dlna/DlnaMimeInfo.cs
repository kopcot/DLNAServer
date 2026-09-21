namespace DlnaServer.Core.Dlna
{
    /// <summary>
    /// Everything the protocol layer needs to know about one <see cref="DlnaMime"/>.
    /// </summary>
    /// <param name="Mime">The enum member this describes.</param>
    /// <param name="MimeString">Value emitted as <c>Content-Type</c> and inside <c>res@protocolInfo</c>.</param>
    /// <param name="Media">Coarse kind, used to pick a processor and a default UPnP class.</param>
    /// <param name="ProfileNames">
    /// DLNA.ORG_PN profile names, most-preferred first. The first entry is what <c>res@protocolInfo</c> carries.
    /// </param>
    /// <param name="FileExtensions">Extensions conventionally carrying this MIME, leading dot, lower case.</param>
    public sealed record DlnaMimeInfo(
        DlnaMime Mime,
        string MimeString,
        DlnaMedia Media,
        IReadOnlyList<string> ProfileNames,
        IReadOnlyList<string> FileExtensions)
    {
        /// <summary>
        /// The DLNA.ORG_PN profile advertised for this MIME, or null when it has none.
        /// </summary>
        public string? MainProfileName => ProfileNames.Count > 0 ? ProfileNames[0] : null;
    }
}
