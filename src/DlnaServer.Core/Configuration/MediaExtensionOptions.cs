using System.ComponentModel.DataAnnotations;

namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// The DLNA MIME and profile advertised for one file extension.
    /// </summary>
    public sealed class MediaExtensionOptions
    {
        /// <summary>
        /// Name of a <c>DlnaMime</c> member.
        /// </summary>
        [Required]
        public string Mime { get; set; } = string.Empty;

        /// <summary>
        /// DLNA.ORG_PN profile name. Null falls back to the MIME's own default profile.
        /// </summary>
        public string? ProfileName { get; set; }
    }
}
