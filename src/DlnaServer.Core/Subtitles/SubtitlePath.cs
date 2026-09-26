using DlnaServer.Core.Dlna;
using DlnaServer.Core.Files;

namespace DlnaServer.Core.Subtitles
{
    /// <summary>
    /// The one rule for where a subtitle linked to a media file may live: in the media file's own folder,
    /// or one folder below it, and nowhere the library hides.
    /// </summary>
    /// <remarks>
    /// Applied when the operator adds a link and again when a subtitle is served, because the stored path
    /// came from what someone typed.
    /// </remarks>
    public static class SubtitlePath
    {
        private const int MaxSegments = 2;

        /// <summary>
        /// Normalises a path given relative to the media file's folder and checks it against the rule.
        /// </summary>
        /// <param name="mediaDirectory">The media file's folder, as an absolute path.</param>
        /// <param name="input">What the operator typed, or what was stored.</param>
        /// <param name="hiddenFolders">
        /// Everything the library hides from listings - <c>ExcludeFolders</c> and the temporarily hidden
        /// folders that are not currently shown.
        /// </param>
        /// <param name="subtitleTypes">
        /// <c>Library.SubtitleFileExtensions</c>, keyed case-insensitively - a type no longer listed is refused
        /// here, so a link to one stops being served the moment the setting changes.
        /// </param>
        /// <param name="relativePath">The path with forward slashes, the form that is stored.</param>
        /// <param name="fullPath">The path on this machine.</param>
        /// <param name="problem">Why the path cannot be used, in the operator's terms.</param>
        public static bool TryResolve(
            string mediaDirectory,
            string? input,
            IReadOnlyList<string> hiddenFolders,
            IReadOnlyDictionary<string, DlnaMedia> subtitleTypes,
            out string relativePath,
            out string fullPath,
            out string problem)
        {
            ArgumentNullException.ThrowIfNull(hiddenFolders);
            ArgumentNullException.ThrowIfNull(subtitleTypes);

            relativePath = string.Empty;
            fullPath = string.Empty;
            problem = string.Empty;

            var trimmed = input?.Trim().Replace('\\', '/') ?? string.Empty;
            var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
            {
                problem = "Give the subtitle file's name.";
            }
            else if (trimmed.StartsWith('/') || Path.IsPathRooted(trimmed) || trimmed.Contains(':'))
            {
                problem = "Give the path from this file's folder, not from the top of the disc.";
            }
            else if (Array.Exists(segments, static s => s is "." or ".."))
            {
                problem = "The subtitle has to be in this file's folder or one folder below it.";
            }
            else if (Array.Exists(segments, static s => s.EndsWith('.') || s.EndsWith(' ') || s.Contains('~')))
            {
                // Windows drops a trailing dot or space and resolves an 8.3 name, so either would reach a
                // file under another spelling than the one the hidden-folder test just read.
                problem = "A name ending in a dot or a space, or containing '~', cannot be used.";
            }
            else if (segments.Length > MaxSegments)
            {
                problem = "Only one folder down is looked at - move the subtitle up, or next to the file.";
            }
            else if (!SubtitleMatcher.IsLinkablePath(segments[^1], subtitleTypes))
            {
                problem = "That is not a subtitle or lyrics type listed under Subtitle types on the Settings page.";
            }

            if (problem.Length > 0)
            {
                return false;
            }

            relativePath = string.Join('/', segments);
            fullPath = Path.Combine([mediaDirectory, .. segments]);

            if (PathExclusion.IsHidden(fullPath, hiddenFolders))
            {
                problem = "That folder is hidden from the library.";

                return false;
            }

            return true;
        }

        /// <summary>
        /// <see cref="TryResolve"/>, and then the disc: the path has to name a file that exists and is
        /// reached without following a link - the one check both adding a link and serving one make.
        /// </summary>
        /// <param name="mediaDirectory">The media file's folder, as an absolute path.</param>
        /// <param name="input">What the operator typed, or what was stored.</param>
        /// <param name="hiddenFolders">What the caller hides - see <see cref="TryResolve"/>.</param>
        /// <param name="subtitleTypes">The configured subtitle types - see <see cref="TryResolve"/>.</param>
        /// <param name="relativePath">The path with forward slashes, the form that is stored.</param>
        /// <param name="fullPath">The path on this machine.</param>
        /// <param name="problem">Why the path cannot be used, in the operator's terms.</param>
        public static bool TryLocate(
            string mediaDirectory,
            string? input,
            IReadOnlyList<string> hiddenFolders,
            IReadOnlyDictionary<string, DlnaMedia> subtitleTypes,
            out string relativePath,
            out string fullPath,
            out string problem)
        {
            if (!TryResolve(mediaDirectory, input, hiddenFolders, subtitleTypes, out relativePath, out fullPath, out problem))
            {
                return false;
            }

            if (!ExistsWithoutLinks(fullPath, relativePath))
            {
                problem = "There is no such file there, or it is reached through a link.";

                return false;
            }

            return true;
        }

        /// <summary>
        /// Whether a path <see cref="TryResolve"/> accepted names an ordinary file that is reached
        /// without following a link.
        /// </summary>
        /// <remarks>
        /// <see cref="TryResolve"/> is lexical, and the disc is not: a subtitle swapped for a symlink - or
        /// kept in a linked sub-folder - spells like an ordinary path and would serve whatever it points
        /// at. The media folder itself is not tested; the scanner already refused to index through a link.
        /// </remarks>
        private static bool ExistsWithoutLinks(string fullPath, string relativePath)
        {
            var file = new FileInfo(fullPath);

            if (!file.Exists || file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return false;
            }

            return !relativePath.Contains('/')
                || file.Directory is not { } folder
                || !folder.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
    }
}
