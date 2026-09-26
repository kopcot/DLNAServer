using DlnaServer.Core.Subtitles;

namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// A subtitle or lyrics file linked to a media file.
    /// </summary>
    public sealed record SubtitleFileDto
    {
        public required Guid PublicId { get; init; }

        /// <summary>
        /// The media file it is linked to.
        /// </summary>
        public required Guid MediaFilePublicId { get; init; }

        /// <summary>
        /// The media file's full path, which <see cref="RelativePath"/> is resolved from.
        /// </summary>
        public required string MediaFileFullPath { get; init; }

        /// <summary>
        /// Where it is, from the media file's folder, with forward slashes - <c>film.en.srt</c> or
        /// <c>Subs/film.en.srt</c>.
        /// </summary>
        public required string RelativePath { get; init; }

        public string? Language { get; init; }

        public required SubtitleSource Source { get; init; }
    }
}
