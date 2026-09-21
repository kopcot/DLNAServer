using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// What to look for when searching the indexed library.
    /// </summary>
    /// <remarks>
    /// Every filter is optional and they combine with AND. All unset means "everything", bounded by
    /// <see cref="Take"/> - which is why that has a value rather than being nullable: a search with no
    /// criteria must not try to return a 25,000-file library.
    /// </remarks>
    public sealed record MediaFileSearchRequest
    {
        /// <summary>
        /// Matches anywhere in the file name, ignoring case.
        /// </summary>
        /// <remarks>
        /// Case folding is the database's, which for SQLite's <c>LIKE</c> covers ASCII only - so
        /// <c>naruto</c> finds <c>Naruto</c>, but an accented capital is not folded to its lower-case
        /// form. Worth knowing on a library with non-English titles.
        /// </remarks>
        public string? NameContains { get; init; }

        /// <summary>
        /// Restricts to video, audio or images.
        /// </summary>
        public DlnaMedia? Media { get; init; }

        /// <summary>
        /// Restricts to one exact MIME type, which is narrower than <see cref="Media"/>.
        /// </summary>
        public DlnaMime? Mime { get; init; }

        /// <summary>
        /// Earliest file modification time, inclusive.
        /// </summary>
        public DateTime? ModifiedFromUtc { get; init; }

        /// <summary>
        /// Latest file modification time, inclusive.
        /// </summary>
        public DateTime? ModifiedToUtc { get; init; }

        public long? MinSizeInBytes { get; init; }

        public long? MaxSizeInBytes { get; init; }

        /// <summary>
        /// Smallest acceptable video width, from the extracted metadata.
        /// </summary>
        /// <remarks>
        /// Only matches files whose metadata has been read: a file still waiting for the processing pass
        /// has no width to compare, so it cannot satisfy this and is excluded rather than assumed.
        /// </remarks>
        public int? MinWidth { get; init; }

        public int? MinHeight { get; init; }

        /// <summary>
        /// Matches part of the audio or video codec name, ignoring case.
        /// </summary>
        public string? CodecContains { get; init; }

        /// <summary>
        /// Keeps files carrying an audio track in any of these languages. Empty means any.
        /// </summary>
        /// <remarks>
        /// The languages combine with OR - a film is wanted if it has <em>one</em> of the selected dubs -
        /// while the filter as a whole still combines with the others by AND. Values are the container's
        /// own language tags, usually ISO 639-2 (<c>eng</c>, <c>ces</c>), and are compared exactly:
        /// they come from <see cref="MediaLanguagesDto"/>, which lists what the index actually holds.
        /// </remarks>
        public IReadOnlyList<string> AudioLanguages { get; init; } = [];

        /// <summary>
        /// Keeps files carrying a subtitle track in any of these languages. Empty means any.
        /// </summary>
        public IReadOnlyList<string> SubtitleLanguages { get; init; } = [];

        /// <summary>
        /// Keeps only files whose details have been read, or only those still without them. Unset means
        /// both.
        /// </summary>
        /// <remarks>
        /// Decided on the recorded metadata stamp, so "without details" covers a file the processing pass
        /// has not reached yet <em>and</em> one whose every attempt failed - both are genuinely missing
        /// their details, which is what an operator looking for work to do wants to see.
        /// </remarks>
        public bool? HasMetadata { get; init; }

        /// <summary>
        /// Keeps only files whose preview image has been made, or only those still without one. Unset
        /// means both.
        /// </summary>
        /// <remarks>
        /// Decided on the thumbnail stamp rather than on the presence of a thumbnail row, so a file that
        /// can never have a preview is not reported as missing one: audio is stamped without anything
        /// being generated, and reading the row instead would fill the results with an entire music
        /// collection that no amount of reprocessing will change.
        /// </remarks>
        public bool? HasThumbnail { get; init; }

        /// <summary>
        /// Keeps only files the byte cache may hold, or only those barred from it. Unset means both.
        /// </summary>
        /// <remarks>
        /// Written in the direction the operator reads it, which is the opposite of the column: a file is
        /// kept in memory unless reading it once failed, and the negation lives in the one place that
        /// compares it. What this finds is the files that have failed - a share that went away, a
        /// permission since corrected - which is the list worth having before letting them back in.
        /// </remarks>
        public bool? IsKeptInMemory { get; init; }

        /// <summary>
        /// Most results to return.
        /// </summary>
        public int Take { get; init; } = 200;
    }
}
