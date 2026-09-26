using DlnaServer.Core.Dlna;

namespace DlnaServer.Host.Delivery
{
    /// <summary>
    /// The <c>Content-Type</c> a linked subtitle or lyrics file is served with, by either port.
    /// </summary>
    internal static class SubtitleContentType
    {
        private const string Fallback = "text/plain";

        /// <summary>
        /// The catalog's type for the file's extension, or <c>text/plain</c> when the catalog has none.
        /// </summary>
        /// <param name="fullPath">The subtitle's path; only its extension is read.</param>
        /// <param name="mime">The catalog's MIME, or <see cref="DlnaMime.Undefined"/> when it has none.</param>
        public static string For(string fullPath, out DlnaMime mime)
        {
            return DlnaMimeCatalog.TryGetByFileExtension(Path.GetExtension(fullPath), out mime)
                ? mime.ToMimeString()
                : Fallback;
        }
    }
}
