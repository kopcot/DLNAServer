using DlnaServer.Core.Files;
using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DlnaServer.Persistence.Repositories
{
    /// <summary>
    /// The SQL half of <see cref="PathExclusion"/> - one home for the exclusion predicate and the LIKE
    /// escaping it depends on.
    /// </summary>
    /// <remarks>
    /// This predicate existed in <b>three</b> places, each carrying a comment telling the next reader to
    /// keep them in step: once over <see cref="MediaFileEntity"/> in <c>MediaFileRepository</c>, and
    /// twice in <c>MediaDirectoryRepository</c> - over <see cref="MediaDirectoryEntity"/> and again over
    /// <see cref="MediaFileEntity"/>, byte-identical to the first. They stopped being in step, which
    /// reopened a customer-reported bug: see <see cref="PathExclusion.Canonicalise"/> for the mechanism.
    /// Three hand-synced copies is how a rule stops being one rule.
    /// <para>
    /// The stated reason for the duplication - that "an expression tree cannot be handed across" - was
    /// <b>wrong</b>. Both repositories compile into this same assembly, and composing
    /// <see cref="Queryable.Where{TSource}(IQueryable{TSource}, System.Linq.Expressions.Expression{Func{TSource, bool}})"/>
    /// from a shared method is ordinary LINQ. An expander is needed only to <i>nest</i> one lambda inside
    /// another, which is true of <c>GetWithDetailsAsync</c>'s projection and of nothing here.
    /// </para>
    /// <para>
    /// The two file-entity copies collapse into <see cref="ExcludeHiddenFiles"/>. The directory arm stays
    /// separate rather than becoming generic over an interface: EF Core translates member access through
    /// the CLR member declared on the entity, and an interface member reached through a generic
    /// constraint is not reliably translated - a collapse that might throw "could not be translated" on
    /// the browse path is not an improvement. Both arms share the pattern building and the escaping,
    /// which is where the divergence actually happened.
    /// </para>
    /// </remarks>
    internal static class HiddenPathQuery
    {
        /// <summary>
        /// The character that escapes a literal <c>%</c> or <c>_</c> inside a LIKE pattern.
        /// </summary>
        /// <remarks>
        /// A backslash, and the patterns are built over paths normalised to <c>/</c> precisely so no
        /// literal backslash can reach the pattern and be read as an escape sequence.
        /// </remarks>
        internal const string LikeEscape = "\\";

        private const string PosixSeparator = "/";

        /// <summary>
        /// Drops every file that is, or lies under, one of the configured excluded folders.
        /// </summary>
        internal static IQueryable<MediaFileEntity> ExcludeHiddenFiles(
            IQueryable<MediaFileEntity> query,
            IList<string> excludedEntries)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(excludedEntries);

            for (var index = 0; index < excludedEntries.Count; index++)
            {
                if (BuildPattern(excludedEntries[index]) is not { } pattern)
                {
                    continue;
                }

                // Wrapping both sides in a separator is what makes the match segment-aligned in a single
                // LIKE: '/a/b/' contains '/b/' but not '/bL/'. The stored path is normalised at the same
                // time, because it carries the separator of whichever host indexed it.
                //
                // Written out rather than calling a helper: EF Core would have to translate the call, and
                // a private method has no SQL translation - it either throws or falls back to evaluating
                // the whole predicate on the client, which would pull every row.
                query = query.Where(f => !EF.Functions.Like(
                    PosixSeparator + f.FullPath.Replace("\\", PosixSeparator) + PosixSeparator,
                    pattern,
                    LikeEscape));
            }

            return query;
        }

        /// <summary>
        /// Drops every folder that is, or lies under, one of the configured excluded folders.
        /// </summary>
        /// <remarks>
        /// A folder's own name is part of its path, so the same pattern hides both an excluded folder and
        /// everything beneath it.
        /// </remarks>
        internal static IQueryable<MediaDirectoryEntity> ExcludeHiddenDirectories(
            IQueryable<MediaDirectoryEntity> query,
            IList<string> excludedEntries)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(excludedEntries);

            for (var index = 0; index < excludedEntries.Count; index++)
            {
                if (BuildPattern(excludedEntries[index]) is not { } pattern)
                {
                    continue;
                }

                // Same shape as the file arm above, and written out for the same reason.
                query = query.Where(d => !EF.Functions.Like(
                    PosixSeparator + d.FullPath.Replace("\\", PosixSeparator) + PosixSeparator,
                    pattern,
                    LikeEscape));
            }

            return query;
        }

        /// <summary>
        /// Drops every preview whose media file is, or lies under, one of the given folders.
        /// </summary>
        /// <remarks>
        /// Keyed off the media file's path rather than the preview's own, because a preview lives in a
        /// sub-folder of the media it belongs to and that sub-folder is itself excluded from scanning -
        /// so matching on <see cref="ThumbnailEntity.FilePath"/> would test the wrong path against the
        /// wrong rule. Used only by delivery, where a preview of hidden content would otherwise be the
        /// one thing about it still reachable.
        /// </remarks>
        internal static IQueryable<ThumbnailEntity> ExcludeHiddenThumbnails(
            IQueryable<ThumbnailEntity> query,
            IList<string> excludedEntries)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(excludedEntries);

            for (var index = 0; index < excludedEntries.Count; index++)
            {
                if (BuildPattern(excludedEntries[index]) is not { } pattern)
                {
                    continue;
                }

                // Same shape as the two arms above, and written out for the same reason. A preview with
                // no media file left is kept: reconciliation owns that row, and hiding must never be what
                // decides an orphan's fate.
                query = query.Where(t => t.MediaFile == null || !EF.Functions.Like(
                    PosixSeparator + t.MediaFile.FullPath.Replace("\\", PosixSeparator) + PosixSeparator,
                    pattern,
                    LikeEscape));
            }

            return query;
        }

        /// <summary>
        /// Makes a caller's term safe to put inside a LIKE pattern.
        /// </summary>
        /// <remarks>
        /// Without this, a term containing <c>%</c> matches everything and one containing <c>_</c>
        /// matches any single character - so a term with a literal underscore, which filenames are full
        /// of, would quietly return the wrong rows rather than fail. The escape character is escaped
        /// first, or escaping <c>%</c> and <c>_</c> would double-escape the ones it introduced.
        /// </remarks>
        internal static string EscapeLikeTerm(string term)
        {
            ArgumentNullException.ThrowIfNull(term);

            return term
                .Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
                .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
                .Replace("_", LikeEscape + "_", StringComparison.Ordinal);
        }

        /// <remarks>
        /// Null for an entry that excludes nothing, so a caller skips it rather than emitting a pattern
        /// that would match every row or none.
        /// </remarks>
        private static string? BuildPattern(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                return null;
            }

            // The one canonical form, shared with the in-memory matcher. Building the pattern from the
            // raw entry is what M1 was: a configured "/path1/" became %//path1//%, which needs a doubled
            // separator no stored path holds, so the entry silently hid nothing at all.
            var canonical = PathExclusion.Canonicalise(entry);

            return canonical.Length == 0
                ? null
                : $"%{PosixSeparator}{EscapeLikeTerm(canonical)}{PosixSeparator}%";
        }
    }
}
