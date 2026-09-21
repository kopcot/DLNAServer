using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Contracts.Scanning
{
    /// <summary>
    /// A resolved extension mapping: which MIME to advertise, and optionally which DLNA profile.
    /// </summary>
    /// <param name="Mime">MIME advertised for files with this extension.</param>
    /// <param name="DlnaProfileName">
    /// DLNA.ORG_PN profile, or null to fall back to the MIME's default profile.
    /// </param>
    public sealed record MediaExtensionMapping(DlnaMime Mime, string? DlnaProfileName);
}
