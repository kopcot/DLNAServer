using System.Globalization;

namespace DlnaServer.Core.Files
{
    /// <summary>
    /// Identifies a file's content cheaply, so a changed file can be detected without reading it.
    /// </summary>
    /// <remarks>
    /// Size plus last-write time. Not a hash: hashing 20,000 media files on every scan would cost far more
    /// than it is worth, and the pair catches every realistic edit. The reference had no equivalent, so a
    /// replaced file kept its original metadata and thumbnail indefinitely.
    /// </remarks>
    public static class ContentStamp
    {
        /// <summary>
        /// Builds the stamp stored against a file and compared on later scans.
        /// </summary>
        public static string From(long sizeInBytes, DateTime lastWriteUtc)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{sizeInBytes}:{lastWriteUtc.Ticks}");
        }
    }
}
