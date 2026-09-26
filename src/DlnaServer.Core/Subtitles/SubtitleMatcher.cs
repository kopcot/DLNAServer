using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Subtitles
{
    /// <summary>
    /// Decides which subtitle and lyrics files in a folder belong to which media file, by name alone.
    /// </summary>
    /// <remarks>
    /// A subtitle belongs to a media file when its name without the extension is the media file's name
    /// without the extension, or that name followed by a dot and anything - <c>film.srt</c>,
    /// <c>film.en.srt</c>, <c>film.1.en.srt</c> for <c>film.mkv</c>. When several media names fit, the
    /// longest wins, so <c>film.1.en.srt</c> goes to <c>film.1.mkv</c> when that file exists. Which
    /// extensions count, and what each goes with, is <c>Library.SubtitleFileExtensions</c>, handed in by the
    /// caller: a type listed for video only ever matches video, and one listed for music only music.
    /// </remarks>
    public static class SubtitleMatcher
    {
        private const string VobSubIndexExtension = ".idx";
        private const string MicroDvdExtension = ".sub";

        // Words that describe a subtitle or a release rather than name its language - film.en.forced.srt is
        // English, and film.Extended.Cut.srt has none. ponytail: a deny-list, so an unlisted release tag
        // still reads as a language; an ISO 639 allow-list is the upgrade if that shows up in real names.
        private static readonly string[] _descriptiveWords = ["forced", "sdh", "cc", "hi", "default", "cut", "dc", "hd", "uhd"];

        /// <summary>
        /// Whether a scan should report a file with this extension: every linkable extension, plus the
        /// VobSub index that tells a picture-based <c>.sub</c> apart from a text one.
        /// </summary>
        /// <param name="extension">The file's extension, with its leading dot.</param>
        /// <param name="subtitleTypes">The configured subtitle types, keyed case-insensitively.</param>
        public static bool IsScanned(string extension, IReadOnlyDictionary<string, DlnaMedia> subtitleTypes)
        {
            return IsLinkable(extension, subtitleTypes)
                || extension.Equals(VobSubIndexExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether a file with this extension can be linked to a media file as its subtitle or lyrics.
        /// </summary>
        /// <param name="extension">The file's extension, with its leading dot.</param>
        /// <param name="subtitleTypes">The configured subtitle types, keyed case-insensitively.</param>
        public static bool IsLinkable(string extension, IReadOnlyDictionary<string, DlnaMedia> subtitleTypes)
        {
            ArgumentNullException.ThrowIfNull(subtitleTypes);

            return subtitleTypes.ContainsKey(extension);
        }

        /// <summary>
        /// Pairs every linkable file in <paramref name="fileNames"/> with the media file it belongs to.
        /// </summary>
        /// <param name="media">The media files of one folder.</param>
        /// <param name="fileNames">The subtitle-like file names of the same folder, without folders.</param>
        /// <param name="subtitleTypes">
        /// The configured subtitle types, keyed case-insensitively, each with the kind of media it goes with.
        /// </param>
        public static IReadOnlyList<SubtitleMatch<TKey>> Match<TKey>(
            IReadOnlyList<SubtitleMedia<TKey>> media,
            IReadOnlyCollection<string> fileNames,
            IReadOnlyDictionary<string, DlnaMedia> subtitleTypes)
        {
            ArgumentNullException.ThrowIfNull(media);
            ArgumentNullException.ThrowIfNull(fileNames);
            ArgumentNullException.ThrowIfNull(subtitleTypes);

            var vobSubStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var fileName in fileNames)
            {
                if (Path.GetExtension(fileName).Equals(VobSubIndexExtension, StringComparison.OrdinalIgnoreCase))
                {
                    vobSubStems.Add(Path.GetFileNameWithoutExtension(fileName));
                }
            }

            // Once per call rather than once per subtitle and media pair.
            var mediaStems = new string[media.Count];

            for (var index = 0; index < media.Count; index++)
            {
                mediaStems[index] = Path.GetFileNameWithoutExtension(media[index].FileName);
            }

            var matches = new List<SubtitleMatch<TKey>>();
            var winners = new List<TKey>();

            foreach (var fileName in fileNames)
            {
                var extension = Path.GetExtension(fileName);
                var stem = Path.GetFileNameWithoutExtension(fileName);

                // A .sub beside an .idx of the same name is VobSub: pictures, not text.
                if (!subtitleTypes.TryGetValue(extension, out var kind)
                    || (extension.Equals(MicroDvdExtension, StringComparison.OrdinalIgnoreCase)
                        && vobSubStems.Contains(stem)))
                {
                    continue;
                }

                var bestLength = -1;

                winners.Clear();

                for (var index = 0; index < media.Count; index++)
                {
                    var candidate = media[index];
                    var mediaStem = mediaStems[index];

                    if (candidate.Kind != kind || mediaStem.Length < bestLength || !BelongsTo(stem, mediaStem))
                    {
                        continue;
                    }

                    if (mediaStem.Length > bestLength)
                    {
                        bestLength = mediaStem.Length;
                        winners.Clear();
                    }

                    winners.Add(candidate.Key);
                }

                if (winners.Count == 0)
                {
                    continue;
                }

                var language = GuessLanguage(stem.AsSpan(bestLength));

                foreach (var winner in winners)
                {
                    matches.Add(new SubtitleMatch<TKey>(winner, fileName, language));
                }
            }

            return matches;
        }

        /// <summary>
        /// The language a subtitle's name suggests, read from what follows the media file's name: the last
        /// dot-separated word of two or three letters, optionally followed by digits.
        /// </summary>
        /// <param name="suffix">The part of the subtitle's name after the media file's name.</param>
        public static string? GuessLanguage(ReadOnlySpan<char> suffix)
        {
            while (!suffix.IsEmpty)
            {
                var dot = suffix.LastIndexOf('.');
                var word = suffix[(dot + 1)..];

                suffix = dot < 0
                    ? ReadOnlySpan<char>.Empty
                    : suffix[..dot];

                if (!IsDescriptive(word) && TryReadLanguage(word, out var language))
                {
                    return language;
                }
            }

            return null;
        }

        /// <summary>
        /// The language a subtitle's name suggests for one media file, or null when the subtitle's name is
        /// not that media file's own.
        /// </summary>
        /// <param name="subtitleFileName">The subtitle's name; a folder in front of it is ignored.</param>
        /// <param name="mediaFileName">The media file's name.</param>
        public static string? GuessLanguage(string subtitleFileName, string mediaFileName)
        {
            var stem = Path.GetFileNameWithoutExtension(subtitleFileName);
            var mediaStem = Path.GetFileNameWithoutExtension(mediaFileName);

            return BelongsTo(stem, mediaStem)
                ? GuessLanguage(stem.AsSpan(mediaStem.Length))
                : null;
        }

        private static bool BelongsTo(string subtitleStem, string mediaStem)
        {
            return subtitleStem.Equals(mediaStem, StringComparison.OrdinalIgnoreCase)
                || (subtitleStem.Length > mediaStem.Length
                    && subtitleStem[mediaStem.Length] == '.'
                    && subtitleStem.StartsWith(mediaStem, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsDescriptive(ReadOnlySpan<char> word)
        {
            foreach (var descriptive in _descriptiveWords)
            {
                if (word.Equals(descriptive, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadLanguage(ReadOnlySpan<char> word, out string? language)
        {
            var letters = 0;

            while (letters < word.Length && char.IsAsciiLetter(word[letters]))
            {
                letters++;
            }

            var isLanguage = letters is 2 or 3 && word[letters..].IndexOfAnyExceptInRange('0', '9') < 0;

            language = isLanguage
                ? word[..letters].ToString().ToLowerInvariant()
                : null;

            return isLanguage;
        }
    }
}
