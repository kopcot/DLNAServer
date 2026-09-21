using System.ComponentModel.DataAnnotations;

namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// Whether the admin pages accept files into the media folders, and where they may land.
    /// </summary>
    /// <remarks>
    /// This is the only part of the server that writes into a source folder, so it ships off and the two
    /// limits below are the whole of the control an operator has over it. Turning it on is deliberately a
    /// restart: the endpoint is mapped at startup or not at all, so a server whose settings say uploads
    /// are off has no route that could accept one, rather than a route that checks a flag.
    /// </remarks>
    public sealed class UploadOptions
    {
        /// <summary>
        /// Accept uploads at all. Takes effect when the server restarts.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// The one folder uploads may reach. Empty means any configured source folder may be chosen.
        /// </summary>
        /// <remarks>
        /// A full path, and it must be inside a source folder - a destination the library never scans
        /// would accept files and then never show them.
        /// </remarks>
        public string? DestinationFolder { get; set; }

        /// <summary>
        /// Largest single file that will be accepted.
        /// </summary>
        /// <remarks>
        /// The request is refused at this size rather than truncated, and the limit applies per file
        /// rather than per batch. The default is large because the files this server exists to serve are
        /// films; it is here so that a mistake costs a rejection instead of a full disc.
        /// </remarks>
        [Range(1, 1_000_000)]
        public int MaxSizeInMegabytes { get; set; } = 8192;
    }
}
