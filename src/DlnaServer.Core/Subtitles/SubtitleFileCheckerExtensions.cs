using DlnaServer.Core.Contracts;

namespace DlnaServer.Core.Subtitles
{
    /// <summary>
    /// Which of a media file's subtitle links can actually be handed out.
    /// </summary>
    public static class SubtitleFileCheckerExtensions
    {
        /// <summary>
        /// The relative paths of the links <paramref name="checker"/> accepts, in the order given, each once.
        /// </summary>
        /// <remarks>
        /// The one rule both the file's page and its subtitle download apply, so the button's wording - one
        /// file or a zip - matches what the download delivers.
        /// </remarks>
        /// <param name="checker">What decides whether a link still works.</param>
        /// <param name="mediaFullPath">The media file the links belong to.</param>
        /// <param name="links">Its stored subtitle links.</param>
        public static List<string> Usable(
            this ISubtitleFileChecker checker,
            string mediaFullPath,
            IReadOnlyList<SubtitleFileDto> links)
        {
            ArgumentNullException.ThrowIfNull(checker);
            ArgumentNullException.ThrowIfNull(links);

            var mediaDirectory = Path.GetDirectoryName(mediaFullPath) ?? string.Empty;
            var usable = new List<string>(links.Count);
            var seen = new HashSet<string>(links.Count, StringComparer.Ordinal);

            foreach (var link in links)
            {
                if (checker.TryCheck(mediaDirectory, link.RelativePath, out var relativePath, out _)
                    && seen.Add(relativePath))
                {
                    usable.Add(relativePath);
                }
            }

            return usable;
        }
    }
}
