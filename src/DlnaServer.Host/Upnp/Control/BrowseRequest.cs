using System.Globalization;

namespace DlnaServer.Host.Upnp.Control
{
    /// <summary>
    /// A Browse request after its arguments have been interpreted.
    /// </summary>
    /// <param name="BrowseMetadata">
    /// True for <c>BrowseMetadata</c> - describe the object itself rather than list its children.
    /// </param>
    /// <param name="StartingIndex">Zero-based offset into the child list.</param>
    /// <param name="RequestedCount">How many objects to return.</param>
    /// <param name="SortDescending">True when the sort criteria asked for descending order.</param>
    /// <param name="SortByDate">True when sorting on <c>dc:date</c> rather than title.</param>
    /// <param name="IncludesAllProperties">True when the filter is <c>*</c> or empty.</param>
    /// <param name="RequestedProperties">Properties named by the filter, when it is not <c>*</c>.</param>
    internal sealed record BrowseRequest(
        bool BrowseMetadata,
        int StartingIndex,
        int RequestedCount,
        bool SortDescending,
        bool SortByDate,
        bool IncludesAllProperties,
        IReadOnlySet<string> RequestedProperties)
    {
        /// <summary>
        /// Interprets the raw SOAP arguments.
        /// </summary>
        /// <remarks>
        /// The reference ignored <c>browseFlag</c>, <c>filter</c> and <c>sortCriteria</c> entirely, and
        /// clamped <c>requestedCount</c> to at least 1 - so a request for zero, which the specification
        /// defines as "all", returned a single item. All four are honoured here.
        /// </remarks>
        public static BrowseRequest Parse(
            string? browseFlag,
            string? filter,
            int startingIndex,
            int requestedCount,
            string? sortCriteria,
            int maximumCount)
        {
            // maximumCount is configuration, and configuration is validated - but this is the value that
            // reaches List<T>.GetRange, where a negative throws and takes every Browse with it. Clamping
            // here costs nothing and means a bad configured value degrades to one item instead of an
            // empty library.
            var ceiling = Math.Max(1, maximumCount);

            var effectiveCount = requestedCount <= 0
                ? ceiling
                : Math.Min(requestedCount, ceiling);

            var (sortByDate, sortDescending) = ParseSort(sortCriteria);

            return new BrowseRequest(
                BrowseMetadata: string.Equals(browseFlag, "BrowseMetadata", StringComparison.OrdinalIgnoreCase),
                StartingIndex: Math.Max(0, startingIndex),
                RequestedCount: effectiveCount,
                SortDescending: sortDescending,
                SortByDate: sortByDate,
                IncludesAllProperties: IsAllProperties(filter),
                RequestedProperties: ParseProperties(filter));
        }

        /// <summary>
        /// Reads the first sort term, which is the only one this server honours.
        /// </summary>
        /// <remarks>
        /// <c>SortCriteria</c> is a comma-separated list, each term prefixed <c>+</c> or <c>-</c>. This
        /// used to be sniffed with two substring tests over the whole string, which got both cases wrong:
        /// <c>-upnp:class</c> contains no <c>-dc:</c> so it sorted ascending, and
        /// <c>"+dc:title,-dc:date"</c> matched both tests at once, so it sorted by date descending when
        /// the renderer asked for title ascending. Only the first term decides, because that is the
        /// primary key and the only one there is anything to sort by here.
        /// </remarks>
        private static (bool ByDate, bool Descending) ParseSort(string? sortCriteria)
        {
            if (string.IsNullOrWhiteSpace(sortCriteria))
            {
                return (false, false);
            }

            var first = sortCriteria.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(first))
            {
                return (false, false);
            }

            var descending = first[0] == '-';
            var property = first.AsSpan(descending || first[0] == '+' ? 1 : 0).Trim();

            return (property.Equals("dc:date", StringComparison.OrdinalIgnoreCase), descending);
        }

        /// <summary>
        /// True when the caller asked for a property, or asked for everything.
        /// </summary>
        public bool Includes(string propertyName)
        {
            return IncludesAllProperties || RequestedProperties.Contains(propertyName);
        }

        private static bool IsAllProperties(string? filter)
        {
            return string.IsNullOrWhiteSpace(filter)
                || filter.Trim() == "*";
        }

        private static HashSet<string> ParseProperties(string? filter)
        {
            if (IsAllProperties(filter))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var properties = filter!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return new HashSet<string>(properties, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Formats a duration the way DIDL-Lite expects, with hours unbounded rather than wrapping at 24.
        /// </summary>
        public static string FormatDuration(TimeSpan duration)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}.{duration.Milliseconds:D3}");
        }
    }
}
