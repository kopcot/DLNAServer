using System.Security.Cryptography;
using System.Text;

namespace DlnaServer.Core.Uploads
{
    /// <summary>
    /// Recognises a browser on its next visit, so the folder it last uploaded into can be offered again.
    /// </summary>
    /// <remarks>
    /// The page and the endpoint that receives the files both compute it, which is why it lives here
    /// rather than in either: a fingerprint spelled two ways would store rows nothing ever reads back.
    /// <para>
    /// Address and user agent together, hashed. It is not an identity and is not treated as one - the
    /// cookie is what normally carries the destination, and this only answers when that is gone. A device
    /// whose address changes looks like a new one and is asked for the folder once more, which is the
    /// harmless direction to be wrong in.
    /// </para>
    /// </remarks>
    public static class UploadDeviceFingerprint
    {
        /// <summary>
        /// Hex, and short enough to read in a log line while still being wide enough not to collide.
        /// </summary>
        private const int LengthInCharacters = 32;

        public static string Create(string? remoteAddress, string? userAgent)
        {
            var material = string.Concat(remoteAddress ?? "unknown", "|", userAgent ?? "unknown");
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));

            return Convert.ToHexString(hash)[..LengthInCharacters];
        }
    }
}
