using System.Globalization;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Uploads
{
    /// <summary>
    /// What a browser is allowed to call a file it sends, and which files are worth accepting.
    /// </summary>
    public static class UploadFileName
    {
        // Names Windows reserves for devices whatever extension follows them. Writing one from a Linux NAS
        // succeeds, and the file then cannot be opened, moved or deleted from a Windows machine on the share.
        private static readonly string[] _reservedDeviceNames =
        [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        ];

        /// <summary>
        /// The name to write, taken from what the browser said. Empty when nothing usable is left.
        /// </summary>
        /// <remarks>
        /// A browser sends the name the user picked, and some send a path with it - a directory upload
        /// sends <c>Holiday/DSC_0001.jpg</c>, and a very old one sends the whole
        /// <c>C:\Users\...\DSC_0001.jpg</c>. Only the last segment is ever used, which is also what makes
        /// the name unable to walk anywhere: there is no separator left in it to walk with.
        /// </remarks>
        public static string Sanitise(string? browserFileName)
        {
            if (string.IsNullOrWhiteSpace(browserFileName))
            {
                return string.Empty;
            }

            var name = browserFileName.Trim().Replace('\\', '/');
            var lastSeparator = name.LastIndexOf('/');

            if (lastSeparator >= 0)
            {
                name = name[(lastSeparator + 1)..];
            }

            name = name.Trim();

            if (name is "" or "." or "..")
            {
                return string.Empty;
            }

            // Everything the local filesystem refuses, plus the two the check above has already ruled out.
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return string.Empty;
            }

            // Control characters too: on Linux the filesystem refuses only '\0' and '/', so a line break
            // sent through filename*= would otherwise reach the disc, the report and every log line.
            // Formatting characters as well - a right-to-left override makes film.exe.mp4 read differently.
            foreach (var character in name)
            {
                if (char.IsControl(character)
                    || CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format)
                {
                    return string.Empty;
                }
            }

            return IsReservedDeviceName(name)
                ? string.Empty
                : name;
        }

        /// <summary>
        /// Whether the library would index a file of this name, which is the only reason to accept one.
        /// </summary>
        /// <remarks>
        /// The same two tiers the scanner resolves a MIME with - configuration first, so the shipped
        /// device quirks win, then the catalog for anything a renderer can actually be offered. It is a
        /// yes-or-no here rather than a mapping because an upload only has to decide whether to take the
        /// file; <c>LibraryScanner.TryResolveMime</c> is what later decides what to call it.
        /// </remarks>
        public static bool IsAcceptedMedia(LibraryOptions library, string fileName)
        {
            ArgumentNullException.ThrowIfNull(library);

            var extension = Path.GetExtension(fileName);

            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            if (library.MediaFileExtensions.TryGetValue(extension, out var configured)
                && Enum.TryParse<DlnaMime>(configured.Mime, ignoreCase: true, out var mime)
                && Enum.IsDefined(mime)
                && mime != DlnaMime.Undefined)
            {
                return true;
            }

            return DlnaMimeCatalog.TryGetByFileExtension(extension, out var fromCatalog)
                && fromCatalog.IsPresentableMedia();
        }

        // CON, CON.mp4 and CON.part1.mp4 are all the device: Windows reads the name up to its first dot,
        // ignoring trailing spaces.
        private static bool IsReservedDeviceName(ReadOnlySpan<char> name)
        {
            var dot = name.IndexOf('.');
            var stem = dot < 0
                ? name
                : name[..dot];

            stem = stem.TrimEnd(' ');

            foreach (var reserved in _reservedDeviceNames)
            {
                if (stem.Equals(reserved, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
