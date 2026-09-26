using DlnaServer.Core.Subtitles;

namespace DlnaServer.Persistence.Entities
{
    /// <summary>
    /// A subtitle or lyrics file linked to a media file - found beside it by a scan, or added by the
    /// operator.
    /// </summary>
    /// <remarks>
    /// Its own table rather than rows in <see cref="SubtitleStreamEntity"/>, which every metadata re-read
    /// clears: an operator's link or language edit must survive re-reading the file.
    /// </remarks>
    internal sealed class SubtitleFileEntity : EntityBase
    {
        public int MediaFileId { get; set; }

        public MediaFileEntity? MediaFile { get; set; }

        /// <summary>
        /// From the media file's folder, with forward slashes - <c>film.en.srt</c> or <c>Subs/film.srt</c>.
        /// Relative, so a film moved together with its subtitles keeps them.
        /// </summary>
        public required string RelativePath { get; set; }

        public string? Language { get; set; }

        public SubtitleSource Source { get; set; }
    }
}
