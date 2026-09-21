using System.Buffers;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;

namespace DlnaServer.Admin.Configuration
{
    /// <summary>
    /// Converts between the configured file-type map and the rows its editor binds to, and decides
    /// whether a set of rows is safe to save.
    /// </summary>
    /// <remarks>
    /// Outside the page because a Razor component cannot be unit tested in this solution, and this is
    /// the half where a mistake is expensive: the map is the only thing that decides whether a file
    /// counts as media, so saving an empty one leaves the server indexing nothing, and a duplicated or
    /// unparseable entry costs one file type silently - the indexer skips what it cannot read and logs
    /// nothing about it.
    /// </remarks>
    internal static class ExtensionMap
    {
        // Characters an extension may not contain past its leading dot.
        private static readonly SearchValues<char> _rejected = SearchValues.Create(" \t\r\n/\\:*?\"<>|");

        /// <summary>
        /// Reads the configured map into editable rows, ordered by extension.
        /// </summary>
        internal static List<ExtensionMapRow> FromOptions(IDictionary<string, MediaExtensionOptions> configured)
        {
            ArgumentNullException.ThrowIfNull(configured);

            var rows = new List<ExtensionMapRow>(configured.Count);

            foreach (var (extension, options) in configured)
            {
                rows.Add(new ExtensionMapRow
                {
                    Extension = extension,

                    // An unreadable stored name becomes "choose a type" rather than a silent default, so
                    // the one thing the editor must not do - hide a broken entry - cannot happen.
                    Mime = Enum.TryParse<DlnaMime>(options.Mime, ignoreCase: true, out var mime)
                        && Enum.IsDefined(mime)
                        ? mime
                        : DlnaMime.Undefined,

                    // Shown filled in rather than blank. A blank profile is not "no profile" - the
                    // server resolves it to the type's default and sends that, so leaving the box empty
                    // hid the value actually going to televisions. Pre-filling it makes the wire value
                    // visible and editable, and changes nothing: it is the same string either way.
                    ProfileName = string.IsNullOrWhiteSpace(options.ProfileName)
                        ? DefaultProfileFor(mime, extension)
                        : options.ProfileName,
                });
            }

            // Ordered so the list does not rearrange itself between visits: a dictionary returns its
            // entries in insertion order and every save rewrites it.
            rows.Sort(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Extension, b.Extension));

            return rows;
        }

        /// <summary>
        /// Every reason these rows cannot be saved, in the operator's language. Empty means they can.
        /// </summary>
        internal static IReadOnlyList<string> Validate(IReadOnlyList<ExtensionMapRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            if (rows.Count == 0)
            {
                return
                [
                    "At least one file type is needed. With none listed, nothing on disc counts as media "
                        + "and the library would be empty.",
                ];
            }

            var problems = new List<string>();
            var seen = new HashSet<string>(rows.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var extension = Normalise(row.Extension);

                if (extension.Length <= 1)
                {
                    problems.Add(
                        "One of the file types has no extension. Give it one, such as .mkv, or remove the line.");

                    continue;
                }

                if (extension.AsSpan(1).ContainsAny(_rejected))
                {
                    problems.Add($"'{extension}' cannot be used as an extension - it contains a space or a slash.");

                    continue;
                }

                if (row.Mime == DlnaMime.Undefined)
                {
                    problems.Add($"'{extension}' has no type chosen, so files ending in it would be ignored.");
                }

                if (string.IsNullOrWhiteSpace(row.ProfileName))
                {
                    problems.Add(
                        $"'{extension}' has no compatibility profile. Pick one from the list, or type your own.");
                }

                if (!seen.Add(extension))
                {
                    problems.Add($"'{extension}' is listed twice. Keep the line you want and remove the other.");
                }
            }

            return problems;
        }

        /// <summary>
        /// The rows in the configuration's own shape, keyed the way the indexer looks an extension up.
        /// </summary>
        /// <remarks>
        /// Only ever called on rows <see cref="Validate"/> has accepted, so a duplicate key here would
        /// overwrite rather than be reported.
        /// </remarks>
        internal static Dictionary<string, MediaExtensionOptions> ToOptions(IReadOnlyList<ExtensionMapRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var map = new Dictionary<string, MediaExtensionOptions>(rows.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var profile = row.ProfileName?.Trim();

                map[Normalise(row.Extension)] = new MediaExtensionOptions
                {
                    Mime = row.Mime.ToString(),

                    // Blank becomes null, not an empty string: null is what makes the indexer fall back
                    // to the type's own default profile, and "" is advertised to a television verbatim.
                    ProfileName = string.IsNullOrEmpty(profile)
                        ? null
                        : profile,
                };
            }

            return map;
        }

        /// <summary>
        /// The profile a television would receive for this type when none is configured.
        /// </summary>
        /// <remarks>
        /// Mirrors <c>DlnaProtocolInfo.ResolveProfile</c>: the type's main profile, or the extension
        /// itself when the type has none. That method is private and lives in an assembly this project
        /// does not reference, so the rule is stated twice - `ExtensionMapTest` pins the two together by
        /// asserting this value is the one that reaches <c>contentFeatures.dlna.org</c>.
        /// </remarks>
        internal static string DefaultProfileFor(DlnaMime mime, string extension)
        {
            return mime.ToMainProfileName()
                ?? Normalise(extension).TrimStart('.').ToUpperInvariant();
        }

        /// <summary>
        /// An extension as the configuration stores it - lower case, with a leading dot.
        /// </summary>
        internal static string Normalise(string? extension)
        {
            var trimmed = extension?.Trim().ToLowerInvariant() ?? string.Empty;

            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            return trimmed.StartsWith('.')
                ? trimmed
                : "." + trimmed;
        }
    }
}
