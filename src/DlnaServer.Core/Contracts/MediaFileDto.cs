using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// A media file as every layer outside persistence sees it.
    /// </summary>
    /// <remarks>
    /// Entities never leave <c>DlnaServer.Persistence</c>; they are internal to that assembly, so the
    /// EF model cannot leak into the protocol, media or admin layers. Repositories project straight
    /// into this record, meaning the emitted SQL selects only these columns.
    /// </remarks>
    public sealed record MediaFileDto
    {
        /// <summary>
        /// Stable external identifier. This is what a renderer receives as a DIDL-Lite ObjectID
        /// and sends back on the next Browse; the database's own integer key never leaves persistence.
        /// </summary>
        public required Guid PublicId { get; init; }

        public required string FullPath { get; init; }

        public required string FileName { get; init; }

        /// <summary>
        /// Title shown to renderers, normally the file name without its extension.
        /// </summary>
        public required string Title { get; init; }

        public required string Extension { get; init; }

        public Guid? DirectoryPublicId { get; init; }

        public required DlnaMime Mime { get; init; }

        /// <summary>
        /// DLNA.ORG_PN profile advertised in <c>res@protocolInfo</c>. Null falls back to the MIME default.
        /// </summary>
        public string? DlnaProfileName { get; init; }

        public required DlnaItemClass UpnpClass { get; init; }

        public required long SizeInBytes { get; init; }

        /// <summary>
        /// How long the file plays for, or null when its metadata has not been read.
        /// </summary>
        /// <remarks>
        /// The video track's duration where there is one, because that is the length of the thing being
        /// watched; an audio-only file falls back to its default track. Carried on this DTO rather than
        /// only on <see cref="MediaFileDetailsDto"/> so a listing can show it without a second read - it
        /// is the one piece of extracted metadata a folder of films is worth reading at a glance.
        /// </remarks>
        public TimeSpan? Duration { get; init; }

        /// <summary>
        /// Pixel width of the video stream, or <see langword="null"/> when there is not one.
        /// </summary>
        /// <remarks>
        /// Carried on the listing DTO rather than fetched per item, because DIDL-Lite's
        /// <c>res@resolution</c> is written for every object in a Browse response and reading it from the
        /// details projection would be one extra query per file on the hot path.
        /// </remarks>
        public int? Width { get; init; }

        /// <summary>
        /// Pixel height of the video stream, or <see langword="null"/> when there is not one.
        /// </summary>
        public int? Height { get; init; }

        /// <summary>
        /// Bits per second reported by the container for the video stream.
        /// </summary>
        /// <remarks>
        /// Stored as the container reports it. DIDL-Lite's <c>res@bitrate</c> is defined in <b>bytes</b>
        /// per second, so the conversion happens where the attribute is written, not here.
        /// </remarks>
        public long? Bitrate { get; init; }

        /// <summary>
        /// Channel count of the default audio track, or <see langword="null"/> when there is not one.
        /// </summary>
        /// <remarks>
        /// Becomes DIDL-Lite's <c>res@nrAudioChannels</c>, which televisions ask for by name in their
        /// Browse filter - an LG requests it alongside <c>res@sampleFrequency</c> and
        /// <c>res@resolution</c>. Carried on the listing DTO for the same reason
        /// <see cref="Width"/> is: the attribute is written for every object in a Browse response, and
        /// reading it from the details projection would be one extra query per file on the hot path.
        /// <para>
        /// "Default" is the track <see cref="Duration"/> already falls back to - flagged default first,
        /// then container order - so every field taken from an audio stream describes the same track.
        /// </para>
        /// </remarks>
        public int? AudioChannels { get; init; }

        /// <summary>
        /// Samples per second of the default audio track, or <see langword="null"/> when there is not one.
        /// </summary>
        /// <remarks>
        /// Becomes DIDL-Lite's <c>res@sampleFrequency</c>.
        /// </remarks>
        public int? AudioSampleRate { get; init; }

        /// <summary>
        /// Codec name of the default audio track, as the container reports it.
        /// </summary>
        /// <remarks>
        /// Becomes <c>upnp:audioCodec</c>. Passed through unchanged rather than mapped to a DLNA profile
        /// name: the element is descriptive, and the profile a renderer actually negotiates on is in
        /// <c>res@protocolInfo</c>.
        /// </remarks>
        public string? AudioCodec { get; init; }

        /// <summary>
        /// Codec name of the video stream, as the container reports it.
        /// </summary>
        /// <remarks>
        /// Becomes <c>upnp:videoCodec</c>.
        /// </remarks>
        public string? VideoCodec { get; init; }

        public required DateTime FileCreatedUtc { get; init; }

        public required DateTime FileModifiedUtc { get; init; }

        public required DateTime CreatedUtc { get; init; }

        /// <summary>
        /// Set when reading this file into the byte cache previously failed.
        /// </summary>
        public required bool IsExcludedFromCache { get; init; }

        /// <summary>
        /// Identifies the file's content as of the last scan. Metadata and thumbnails are regenerated
        /// when their recorded stamp no longer matches this one.
        /// </summary>
        public required string ContentStamp { get; init; }

        public string? MetadataStamp { get; init; }

        public string? ThumbnailStamp { get; init; }

        /// <summary>
        /// Whether metadata extraction is suppressed until explicitly asked for again.
        /// </summary>
        /// <remarks>
        /// On the listing DTO because the processing worker decides from it. Without it the worker could
        /// only compare stamps, and the pending query is an OR across both concerns - so a file returned
        /// for its thumbnail had its metadata re-extracted regardless, quietly undoing a "clear".
        /// </remarks>
        public bool IsMetadataSuppressed { get; init; }

        /// <summary>
        /// Whether thumbnail generation is suppressed until explicitly asked for again.
        /// </summary>
        public bool IsThumbnailSuppressed { get; init; }

        /// <summary>
        /// Whether a thumbnail must be generated rather than adopted from disc.
        /// </summary>
        public bool IsThumbnailRebuildForced { get; init; }

        /// <summary>
        /// How many times reading this file's details has failed.
        /// </summary>
        /// <remarks>
        /// On the listing DTO because it is the only thing separating a file the processing pass has not
        /// reached yet from one that has failed every attempt - both have no stamp, and a search for
        /// files still missing their details returns them together.
        /// </remarks>
        public int MetadataFailureCount { get; init; }

        /// <summary>
        /// How many times making this file's preview image has failed.
        /// </summary>
        public int ThumbnailFailureCount { get; init; }

        /// <summary>
        /// External identifier of the generated thumbnail, when one exists.
        /// </summary>
        public Guid? ThumbnailPublicId { get; init; }
    }
}
