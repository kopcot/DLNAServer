using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;

namespace DlnaServer.Admin.Configuration
{
    /// <summary>
    /// Converts between the configured subtitle types and the rows their editor binds to, and decides
    /// whether a set of rows can be turned back into one.
    /// </summary>
    /// <remarks>
    /// Only what the options validator cannot see is checked here - a blank or repeated line exists only as
    /// rows, since the map they become can hold neither. Everything about a single extension, and a type
    /// that clashes with a file type, is the validator's, which the page runs before it writes.
    /// </remarks>
    internal static class SubtitleTypeMap
    {
        /// <summary>
        /// Reads the configured types into editable rows, ordered by extension.
        /// </summary>
        internal static List<SubtitleTypeRow> FromOptions(IDictionary<string, DlnaMedia> configured)
        {
            ArgumentNullException.ThrowIfNull(configured);

            var rows = new List<SubtitleTypeRow>(configured.Count);

            foreach (var (extension, kind) in configured)
            {
                rows.Add(new SubtitleTypeRow { Extension = extension, Kind = kind });
            }

            // Ordered for the reason ExtensionMap.FromOptions gives: every save rewrites the map.
            rows.Sort(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Extension, b.Extension));

            return rows;
        }

        /// <summary>
        /// The shipped subtitle types as editable rows - a fresh set on every call, because the editor
        /// mutates the rows it is handed.
        /// </summary>
        internal static List<SubtitleTypeRow> Defaults()
        {
            return FromOptions(SubtitleFileExtensionDefaults.Create());
        }

        /// <summary>
        /// Every reason these rows cannot be saved, in the operator's language. Empty means they can.
        /// </summary>
        internal static IReadOnlyList<string> Validate(IReadOnlyList<SubtitleTypeRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            if (rows.Count == 0)
            {
                return
                [
                    "At least one subtitle type is needed - with none listed, the default list comes back the "
                        + "next time the settings are read. To stop sending subtitles, switch off "
                        + "\"Offer subtitles to televisions\" instead.",
                ];
            }

            var problems = new List<string>();
            var seen = new HashSet<string>(rows.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var extension = ExtensionMap.Normalise(row.Extension);

                if (extension.Length == 0)
                {
                    problems.Add("One of the subtitle types has no extension. Give it one, such as .srt, or remove the line.");

                    continue;
                }

                if (!seen.Add(extension))
                {
                    problems.Add($"'{extension}' is listed twice as a subtitle type. Keep the line you want and remove the other.");
                }
            }

            return problems;
        }

        /// <summary>
        /// The rows in the configuration's own shape, each extension normalised the way file types are.
        /// </summary>
        /// <remarks>
        /// Only ever called on rows <see cref="Validate"/> has accepted, so a duplicate key here would
        /// overwrite rather than be reported.
        /// </remarks>
        internal static Dictionary<string, DlnaMedia> ToOptions(IReadOnlyList<SubtitleTypeRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var map = new Dictionary<string, DlnaMedia>(rows.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                map[ExtensionMap.Normalise(row.Extension)] = row.Kind;
            }

            return map;
        }
    }
}
