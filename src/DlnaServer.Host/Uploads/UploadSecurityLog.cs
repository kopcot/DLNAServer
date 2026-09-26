using System.Globalization;
using System.Text;
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
            // Every text value is escaped, not only the file name: the agent and the language are headers
            // the sender wrote, and the reason can quote an exception that repeats the name.
            LogUpload(
                Escape(remoteAddress),
                Escape(userAgent),
                Escape(acceptLanguage),
                Escape(fingerprint),
                Escape(destination),
                Escape(fileName),
                sizeInBytes,
                outcome,
                Escape(reason));
        }

        /// <summary>
        /// Rewrites line breaks, other control characters and double quotes as <c>\uXXXX</c>, so one
        /// upload stays one line.
        /// </summary>
        /// <remarks>
        /// A refused file is logged under the name the browser sent, and <c>filename*=</c> can carry a
        /// percent-encoded line break. Written raw, that name would end the line and start a forged one,
        /// and a stray quote would end the quoted value early - in the one log that exists to be trusted.
        /// </remarks>
        internal static string Escape(string value)
        {
            var index = 0;

            while (index < value.Length && !NeedsEscaping(value[index]))
            {
                index++;
            }

            if (index == value.Length)
            {
                return value;
            }

            var builder = new StringBuilder(value.Length + 16);
            builder.Append(value, 0, index);

            for (; index < value.Length; index++)
            {
                var character = value[index];

                if (NeedsEscaping(character))
                {
                    builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:X4}");
                }
                else
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        // U+2028 and U+2029 are not control characters, but an editor breaks the line on them all the same.
        private static bool NeedsEscaping(char character)
        {
            return char.IsControl(character) || character is '"' or '\u2028' or '\u2029';
        }
    }
}
