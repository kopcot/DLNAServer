using System.ComponentModel.DataAnnotations;

namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// Identity and listening configuration for the UPnP device and its two HTTP endpoints.
    /// </summary>
    public sealed class ServerOptions
    {
        /// <summary>
        /// Port serving UPnP/DLNA and media. This is the port advertised to renderers in SSDP and in <c>description.xml</c>.
        /// </summary>
        [Range(1, 65_535)]
        public int Port { get; set; } = 26_851;

        /// <summary>
        /// Port serving the Blazor admin UI. Kept separate so renderers on the network never reach the admin surface.
        /// </summary>
        [Range(1, 65_535)]
        public int AdminPort { get; set; } = 26_852;

        [Required]
        public string FriendlyName { get; set; } = $"ZEN DLNA Server ({Environment.MachineName})";

        [Required]
        public string ModelName { get; set; } = "1.0.0.0";

        [Required]
        public string ManufacturerName { get; set; } = "Kopco";

        [Required]
        public string ManufacturerUrl { get; set; } = "mailto:kopco.t@gmail.com";

        /// <summary>
        /// Raises every log category to Trace and enables verbose per-request diagnostics.
        /// </summary>
        public bool DebugMode { get; set; }
    }
}
