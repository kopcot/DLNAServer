using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// The subtitle and lyrics types the server ships with - what an unconfigured
    /// <see cref="LibraryOptions.SubtitleFileExtensions"/> is seeded with, and what the Settings page restores.
    /// </summary>
    /// <remarks>
    /// Not a default on the property itself, for the reason <see cref="MediaFileExtensionDefaults"/> gives:
    /// <c>ConfigurationBinder</c> adds to a non-empty collection rather than replacing it, so a default there
    /// would merge back into every operator's own list and a removed type could never stay removed.
    /// </remarks>
    public static class SubtitleFileExtensionDefaults
    {
        // Not .ttml, which the catalog knows: it is served as application/ttml+xml, and an XML document is
        // one a browser opening the media port's URL would render rather than download.
        private static readonly (string Extension, DlnaMedia Kind)[] _entries =
        [
            (".srt", DlnaMedia.Video),
            (".vtt", DlnaMedia.Video),
            (".ass", DlnaMedia.Video),
            (".ssa", DlnaMedia.Video),
            (".sub", DlnaMedia.Video),
            (".smi", DlnaMedia.Video),
            (".lrc", DlnaMedia.Audio),
        ];

        /// <summary>
        /// A fresh map on every call, keyed case-insensitively the way a file's extension is looked up.
        /// </summary>
        /// <remarks>
        /// Fresh because callers mutate what they are given - the Settings editor edits its rows in place.
        /// </remarks>
        public static Dictionary<string, DlnaMedia> Create()
        {
            var map = new Dictionary<string, DlnaMedia>(_entries.Length, StringComparer.OrdinalIgnoreCase);

            foreach (var (extension, kind) in _entries)
            {
                map[extension] = kind;
            }

            return map;
        }
    }
}
