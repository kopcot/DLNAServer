using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// The file-type map the server ships with - what a regenerated <c>config.json</c> is given, and what
    /// the Settings page restores.
    /// </summary>
    /// <remarks>
    /// Not a default on <see cref="LibraryOptions.MediaFileExtensions"/> itself: <c>ConfigurationBinder</c>
    /// adds to a non-empty collection rather than replacing it, so a default there would merge into every
    /// operator's own map. The entries are the shipped <c>config.json</c>'s, <c>.mp3</c> as
    /// <see cref="DlnaMime.AudioMp4"/> included. No profile names: each resolves to its type's own
    /// default, as a blank entry in that file does.
    /// </remarks>
    public static class MediaFileExtensionDefaults
    {
        private static readonly (string Extension, DlnaMime Mime)[] _entries =
        [
            (".mp4", DlnaMime.VideoMp4),
            (".mpg", DlnaMime.VideoMpeg),
            (".mpeg", DlnaMime.VideoMpeg),
            (".avi", DlnaMime.VideoXMsvideo),
            (".mkv", DlnaMime.VideoXMatroska),
            (".mov", DlnaMime.VideoQuicktime),
            (".wmv", DlnaMime.VideoXMswmv),
            (".flv", DlnaMime.VideoXFlv),
            (".m4v", DlnaMime.VideoMpeg),
            (".3gp", DlnaMime.Video3gpp),
            (".mp3", DlnaMime.AudioMp4),
            (".png", DlnaMime.ImagePng),
            (".jpg", DlnaMime.ImageJpeg),
            (".jpeg", DlnaMime.ImageJpeg),
        ];

        /// <summary>
        /// A fresh map on every call, keyed the way the indexer looks an extension up.
        /// </summary>
        /// <remarks>
        /// Fresh because callers mutate what they are given - the Settings editor edits its rows in place.
        /// </remarks>
        public static Dictionary<string, MediaExtensionOptions> Create()
        {
            var map = new Dictionary<string, MediaExtensionOptions>(_entries.Length, StringComparer.OrdinalIgnoreCase);

            foreach (var (extension, mime) in _entries)
            {
                map[extension] = new MediaExtensionOptions { Mime = mime.ToString() };
            }

            return map;
        }
    }
}
