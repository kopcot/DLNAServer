using DlnaServer.Core.Uploads;

namespace DlnaServer.Host.Uploads
{
    /// <summary>
    /// Writes one line per uploaded file to its own log file.
    /// </summary>
    /// <remarks>
    /// <b>This exists for security review, not for diagnosis.</b> Uploading is the only way to put a file
    /// into the media folders through this server, so who sent what, from where, with which browser, and
    /// where it landed has to be answerable afterwards - by someone looking at a file that should not be
    /// there and asking how it arrived. That is why it records more about the sender than the feature
    /// needs to work, and why it is a separate file rather than lines mixed into the application log.
    /// <para>
    /// Routed to <c>logs/uploadSecurity.log</c> by source category, the way the slow-query log is. The
    /// category is this type's full name, which <see cref="Category"/> repeats for the sink to filter on -
    /// a test pins the two together.
    /// </para>
    /// </remarks>
    public sealed partial class UploadSecurityLog
    {
        /// <summary>
        /// The logging category the dedicated sink filters on.
        /// </summary>
        public const string Category = "DlnaServer.Host.Uploads.UploadSecurityLog";

        private readonly ILogger<UploadSecurityLog> _logger;

        public UploadSecurityLog(ILogger<UploadSecurityLog> logger)
        {
            _logger = logger;
        }

        public void Record(
            string remoteAddress,
            string userAgent,
            string acceptLanguage,
            string fingerprint,
            string destination,
            string fileName,
            long sizeInBytes,
            UploadOutcome outcome,
            string reason)
        {
            LogUpload(
                remoteAddress,
                userAgent,
                acceptLanguage,
                fingerprint,
                destination,
                fileName,
                sizeInBytes,
                outcome,
                reason);
        }
    }
}
