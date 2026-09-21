namespace DlnaServer.Core.Files
{
    /// <summary>
    /// Decides whether a path falls inside an excluded folder.
    /// </summary>
    /// <remarks>
    /// An entry is a folder name or a <b>partial path</b> - <c>@Recycle</c> or <c>Films/Private</c> - and
    /// it matches only on whole path-segment boundaries. Both separators are accepted in an entry and
    /// treated as equivalent, so one <c>config.json</c> works on the NAS and on a Windows development
    /// machine, and a database indexed on one and opened on the other still matches.
    /// <para>
    /// <b>Reported by a customer.</b> This used to be two different rules: scanning compared whole
    /// segments, while hiding was a raw substring test over the entire path. So an entry of
    /// <c>path1</c> also hid <c>path1L</c> and <c>path10</c>, an entry containing a separator was
    /// refused by validation outright, and the operator got no indication that far more was hidden
    /// than they had named. Segment-aligned matching is what makes an entry mean the folder the
    /// operator typed and nothing else.
    /// </para>
    /// </remarks>
    public static class PathExclusion
    {
        /// <summary>
        /// True when <paramref name="fullPath"/> is, or lies under, any of
        /// <paramref name="excludedEntries"/>, compared case-insensitively on segment boundaries.
        /// </summary>
        /// <remarks>
        /// The <b>scan</b> side. It shares its rule with <see cref="IsHidden"/>, which it did not before:
        /// the two halves used to disagree on purpose, hiding slightly more than scanning skipped, and
        /// that asymmetry is what let a substring entry hide folders nothing had excluded. A multi-segment
        /// entry also could not match here at all, since nothing compared more than one segment.
        /// <para>
        /// Walks the path as spans rather than calling <c>Split</c>, because this runs once per file on a
        /// 25,000-file scan and an allocation per segment would be pure waste.
        /// </para>
        /// </remarks>
        public static bool IsExcluded(ReadOnlySpan<char> fullPath, IReadOnlyList<string> excludedEntries)
        {
            ArgumentNullException.ThrowIfNull(excludedEntries);

            if (excludedEntries.Count == 0 || fullPath.IsEmpty)
            {
                return false;
            }

            for (var index = 0; index < excludedEntries.Count; index++)
            {
                var entry = excludedEntries[index];

                if (!string.IsNullOrWhiteSpace(entry) && ContainsSegmentRun(fullPath, entry.AsSpan()))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when <paramref name="fullPath"/> is, or lies under, any of
        /// <paramref name="excludedEntries"/>, compared case-insensitively on segment boundaries.
        /// </summary>
        /// <remarks>
        /// The <b>read</b> side of the same setting, and the same rule - it forwards to
        /// <see cref="IsExcluded"/>. Kept as its own method because callers that already hold a
        /// <c>string</c> read better for it. The SQL expression of this rule lives in one place too,
        /// <c>HiddenPathQuery</c>, and derives its entry form from <see cref="Canonicalise"/> rather than
        /// being hand-kept in step.
        /// </remarks>
        public static bool IsHidden(string fullPath, IReadOnlyList<string> excludedEntries)
        {
            // Forwarded directly rather than re-guarding: IsExcluded already rejects an empty path and
            // already null-checks the entries, and a null string converts to an empty span rather than
            // throwing. Duplicating the guards here made the two look like different rules.
            return IsExcluded(fullPath, excludedEntries);
        }

        /// <summary>
        /// The one canonical form of an exclusion entry: surrounding whitespace and separators removed,
        /// and both separators folded to <c>/</c>.
        /// </summary>
        /// <remarks>
        /// <b>This exists because the two halves of the rule drifted and it reopened a customer-reported
        /// bug.</b> The matcher below trimmed an entry; the three SQL predicates in
        /// <c>DlnaServer.Persistence</c> did not. So a configured <c>/path1/</c> built the LIKE pattern
        /// <c>%//path1//%</c>, which needs a doubled separator no stored path contains - the entry
        /// matched nothing, scanning correctly stopped importing the folder, and every row already
        /// indexed stayed visible and streamable. It failed in the exposing direction, and the only
        /// signal was noticing the folder still on the television.
        /// <para>
        /// Both halves now derive from here, so "keep them in step" is structural rather than a comment.
        /// The separator fold is what SQL needs and the matcher does not: <c>LIKE</c> cannot express
        /// "either separator", while <see cref="MatchesAt"/> treats them as equivalent per character.
        /// The <b>trimming</b> - the half that actually diverged - has exactly one definition, in
        /// <see cref="TrimBoundaries"/>, which both sides reach.
        /// </para>
        /// <para>
        /// Matching is <b>ASCII-only for case</b>, on purpose rather than by accident. SQLite's
        /// <c>LIKE</c> folds ASCII and nothing else, so a non-ASCII entry cannot be case-folded on the
        /// SQL side at all; the NAS additionally runs under <c>DOTNET_SYSTEM_GLOBALIZATION_INVARIANT</c>,
        /// which makes the in-memory side ASCII-only too. An entry differing from the folder only in the
        /// case of a non-ASCII letter therefore does not match. Name it as it appears on disc.
        /// </para>
        /// </remarks>
        public static string Canonicalise(string entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            var trimmed = TrimBoundaries(entry.AsSpan());

            // Allocates, and that is fine here: a caller builds one pattern per configured entry per
            // query, not per row. The per-row path is ContainsSegmentRun, which stays span-only.
            return trimmed.IsEmpty
                ? string.Empty
                : trimmed.ToString().Replace('\\', '/');
        }

        /// <summary>
        /// Whether the entry appears in the path as a run of whole segments.
        /// </summary>
        /// <remarks>
        /// Tried at every segment start, so <c>Films/Private</c> matches anywhere in a rooted path. The
        /// match must also END on a boundary, which is what stops <c>path1</c> matching <c>path1L</c>.
        /// </remarks>
        private static bool ContainsSegmentRun(ReadOnlySpan<char> fullPath, ReadOnlySpan<char> entry)
        {
            // Trimmed through the same helper Canonicalise uses, so the scan side and the SQL side cannot
            // disagree about where an entry starts and ends. Span-based rather than calling Canonicalise:
            // this runs once per (file x entry) on a 25,000-file scan, and the string it returns would be
            // pure waste here - the separator fold that string carries is only needed by SQL.
            var trimmed = TrimBoundaries(entry);

            if (trimmed.IsEmpty)
            {
                return false;
            }

            var start = 0;

            while (true)
            {
                if (MatchesAt(fullPath, start, trimmed))
                {
                    return true;
                }

                var next = IndexOfSeparator(fullPath, start);

                if (next < 0)
                {
                    return false;
                }

                start = next + 1;
            }
        }

        /// <remarks>
        /// <paramref name="start"/> is always a segment start - zero, or one past a separator - so only
        /// the trailing boundary needs checking here.
        /// </remarks>
        private static bool MatchesAt(ReadOnlySpan<char> fullPath, int start, ReadOnlySpan<char> entry)
        {
            if (start + entry.Length > fullPath.Length)
            {
                return false;
            }

            for (var index = 0; index < entry.Length; index++)
            {
                var pathChar = fullPath[start + index];
                var entryChar = entry[index];

                // Either separator in the entry matches either separator in the path, so a config written
                // on Windows works against paths indexed on the NAS and the other way round.
                if (StoredPath.IsSeparator(entryChar))
                {
                    if (!StoredPath.IsSeparator(pathChar))
                    {
                        return false;
                    }

                    continue;
                }

                if (char.ToUpperInvariant(pathChar) != char.ToUpperInvariant(entryChar))
                {
                    return false;
                }
            }

            var after = start + entry.Length;

            return after == fullPath.Length || StoredPath.IsSeparator(fullPath[after]);
        }

        private static int IndexOfSeparator(ReadOnlySpan<char> fullPath, int start)
        {
            for (var index = start; index < fullPath.Length; index++)
            {
                if (StoredPath.IsSeparator(fullPath[index]))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <remarks>
        /// The single definition of where an entry begins and ends. <see cref="Canonicalise"/> and
        /// <see cref="ContainsSegmentRun"/> both go through it, which is the whole point: these two
        /// having separate trimming is what let the in-memory rule and the SQL rule diverge.
        /// </remarks>
        private static ReadOnlySpan<char> TrimBoundaries(ReadOnlySpan<char> entry)
        {
            return entry.Trim().Trim(['/', '\\']);
        }
    }
}
