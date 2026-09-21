using System.Collections.Frozen;

namespace DlnaServer.Admin.Playback
{
    /// <summary>
    /// Whether a browser can be expected to present a media file, judged by its container.
    /// </summary>
    /// <remarks>
    /// The admin preview is the only surface that needs this: a television plays whatever its own codecs
    /// cover, and the DLNA side is unaffected by any of it. Before this existed the preview page rendered
    /// a player for every video and every image, so a file the browser cannot decode gave the operator a
    /// poster image, controls that did nothing, and no explanation - 679 files on the live library.
    /// <para>
    /// Judged by the <b>container</b>, deliberately not by the MIME type. Chrome ignores the declared
    /// <c>Content-Type</c> for media and sniffs the bytes: this server sends an MP3 as <c>audio/mp4</c>
    /// on purpose for LG televisions, and it plays perfectly. So no MIME mapping can decide this, which is
    /// recorded in <c>PLAN.md</c> standing decision 20 along with the measurements behind the lists below.
    /// </para>
    /// </remarks>
    internal static class BrowserPlayback
    {
        /// <summary>
        /// Video and audio containers no browser will open.
        /// </summary>
        /// <remarks>
        /// A <b>deny</b>-list, so an unrecognised container still gets a player. That direction is the
        /// point: wrongly hiding a player breaks something that worked, while wrongly showing one is only
        /// the behaviour that already shipped. Measured in Chrome 152 against the live server -
        /// <c>.avi</c>, <c>.wmv</c>, <c>.flv</c> and <c>.mpg</c> all fail with
        /// <c>DEMUXER_ERROR_COULD_NOT_OPEN</c>, while <c>.mp4</c>, <c>.mkv</c>, <c>.mov</c>, <c>.m4v</c>,
        /// <c>.3gp</c> and <c>.flac</c> play. The rest of the entries are the same containers under
        /// another extension, plus RealMedia, which no browser has ever carried.
        /// </remarks>
        private static readonly FrozenSet<string> _unplayableContainers = new[]
        {
            // AVI, measured.
            ".avi", ".divx",

            // ASF, the container .wmv and .wma are both written in. Measured as .wmv.
            ".wmv", ".wma", ".asf",

            ".flv",

            // MPEG program and elementary streams. Measured as .mpg.
            ".mpg", ".mpeg", ".mpe", ".m1v", ".m2v", ".vob",

            ".rm", ".rmvb", ".ra", ".ram",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Image formats a browser will display.
        /// </summary>
        /// <remarks>
        /// An <b>allow</b>-list, the opposite way round from video and audio, because the web image
        /// formats are a short closed set while the formats a browser refuses are not: the built-in MIME
        /// catalogue carries TIFF, PICT, PCX, CMU raster, portable anymap and AutoCAD drawings, and
        /// scanning now infers every one of them from the file extension alone. Listing what works is
        /// both shorter and stable.
        /// </remarks>
        private static readonly FrozenSet<string> _displayableImages = new[]
        {
            ".jpg", ".jpeg", ".jpe", ".jfif",
            ".png", ".gif", ".webp", ".bmp", ".ico", ".svg", ".avif",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Whether a <c>video</c> or <c>audio</c> element can be expected to play this file.
        /// </summary>
        internal static bool CanPlay(string fileExtension)
        {
            return !_unplayableContainers.Contains(fileExtension);
        }

        /// <summary>
        /// Whether an <c>img</c> element can be expected to display this file.
        /// </summary>
        internal static bool CanDisplayImage(string fileExtension)
        {
            return _displayableImages.Contains(fileExtension);
        }

        /// <summary>
        /// The container's name as an operator would recognise it, for a message naming the format.
        /// </summary>
        internal static string ContainerName(string fileExtension)
        {
            return fileExtension.TrimStart('.').ToUpperInvariant();
        }
    }
}
