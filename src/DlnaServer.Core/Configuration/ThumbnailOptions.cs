using System.ComponentModel.DataAnnotations;

namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// Thumbnail generation and storage.
    /// </summary>
    public sealed class ThumbnailOptions
    {
        /// <summary>
        /// Folder name, created beside each media file, that its thumbnail is written into - so
        /// <c>/share/Media/Films/Film.mkv</c> is previewed by
        /// <c>/share/Media/Films/.@__thumb/Film.mkv.jpg</c>.
        /// </summary>
        /// <remarks>
        /// This is the reference's layout and the default because it keeps thumbnails with the media they
        /// describe: they survive a redeploy, and publishing to a new folder does not silently discard the
        /// generated previews for an entire library. A central cache under the application folder loses
        /// all of them on the next deployment.
        /// <para>
        /// Required, and always excluded from indexing whether or not
        /// <see cref="LibraryOptions.ExcludeFolders"/> mentions it - <c>DlnaOptionsDefaults</c> adds it
        /// after binding. Without that, a scan re-ingests the previews as media and then generates
        /// previews of those, which is how a library quietly doubles; leaving the guarantee to an
        /// operator remembering to keep two settings in step is what made it fragile.
        /// </para>
        /// </remarks>
        [Required(AllowEmptyStrings = false)]
        public string SubFolderName { get; set; } = ".@__thumb";

        /// <summary>
        /// Directory holding generated thumbnails when <see cref="SubFolderName"/> is blank.
        /// </summary>
        /// <remarks>
        /// The escape hatch for a read-only media volume, where nothing can be written beside the files.
        /// Must not sit inside a source folder unless an exclusion covers it.
        /// <para>
        /// <b>Currently unreachable</b>: <see cref="SubFolderName"/> is required, so thumbnails always go
        /// beside the media and nothing reads this. It is kept, along with the validation that a cache
        /// inside a source folder is excluded from scanning, because that is what makes restoring the
        /// read-only-volume path a one-line change rather than a redesign.
        /// </para>
        /// </remarks>
        [Required]
        public string CacheDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "thumbnails");

        public bool GenerateForVideo { get; set; } = true;
        public bool GenerateForImages { get; set; } = true;

        /// <summary>
        /// Take an audio file's preview from the cover image embedded in it. Files carrying no cover are
        /// left without one rather than counted as a failure.
        /// </summary>
        public bool GenerateForAudio { get; set; } = true;

        /// <summary>
        /// Also store the thumbnail bytes in the database. Costs disk, saves a file read when serving.
        /// </summary>
        public bool StoreInDatabase { get; set; } = true;

        [Range(16, 4096)]
        public int MaxWidth { get; set; } = 480;

        [Range(16, 4096)]
        public int MaxHeight { get; set; } = 360;

        [Range(1, 100)]
        public int Quality { get; set; } = 75;

        /// <summary>
        /// Fetch ffmpeg on first use when it is not already present.
        /// </summary>
        /// <remarks>
        /// Video metadata and video thumbnails need it. With this off and no binaries present, the
        /// server still indexes, browses and streams - only those two features are skipped.
        /// <para>
        /// <b>Off by default, deliberately.</b> The download resolves whatever archive URL a third-party
        /// API names and neither it nor this server verifies a hash or a signature, after which the
        /// binary is executed as the server user and persists across redeploys - so a compromise of that
        /// host, its CDN or its DNS would be code execution on the NAS, reached without anyone asking for
        /// it. Ship the binaries with the deployment, or turn this on knowing what it does.
        /// </para>
        /// </remarks>
        public bool DownloadFFmpeg { get; set; }
    }
}
