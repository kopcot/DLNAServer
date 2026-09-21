using DlnaServer.Core.Configuration;
using DlnaServer.Core.Contracts;

namespace DlnaServer.Host.Indexing
{
    /// <summary>
    /// Where a file's preview image lives.
    /// </summary>
    /// <remarks>
    /// Extracted from <c>MediaProcessingHostedService</c> when Browse gained a second reason to know the
    /// answer: it warms a folder's previews into the byte cache as it replies. Two copies of this rule
    /// would drift, and the failure would be silent - a wrong path simply prefetches nothing.
    /// </remarks>
    internal static class ThumbnailPath
    {
        /// <summary>
        /// Extension used when the configured thumbnail format names none.
        /// </summary>
        public const string DefaultExtension = ".jpg";

        public static string Resolve(MediaFileDto file, DlnaOptions options, string extension)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentNullException.ThrowIfNull(options);

            var subFolder = options.Thumbnails.SubFolderName;

            if (!string.IsNullOrWhiteSpace(subFolder)
                && Path.GetDirectoryName(file.FullPath) is { Length: > 0 } mediaFolder)
            {
                // Beside the media, as the reference does: Films/.@__thumb/Film.mkv.jpg. The name keeps
                // the media file's own extension, so Film.mkv and Film.mp4 in one folder do not collide.
                // Scanning skips the folder by name, which is what stops these being re-ingested as media.
                return Path.Combine(mediaFolder, subFolder, file.FileName + extension);
            }

            // Central cache: no per-folder home to write into, so two levels of fan-out keep any single
            // directory from holding 20,000 entries, which some filesystems handle badly.
            var key = file.PublicId.ToString("N");

            return Path.Combine(
                options.Thumbnails.CacheDirectory,
                key[..2],
                key[2..4],
                key + extension);
        }
    }
}
