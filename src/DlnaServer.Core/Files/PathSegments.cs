namespace DlnaServer.Core.Files
{
    /// <summary>
    /// Questions asked of the segments of a path as it was typed, before anything resolves it.
    /// </summary>
    public static class PathSegments
    {
        /// <summary>
        /// Whether any segment of the entry is <c>.</c> or <c>..</c>.
        /// </summary>
        /// <remarks>
        /// Either separator ends a segment. Walked by hand rather than with <c>Split</c>, matching
        /// <see cref="PathExclusion"/>: the enumerable span split is a .NET 9 API and <c>global.json</c>
        /// pins the 8.0 SDK, so it would not compile here.
        /// </remarks>
        public static bool HasDotSegment(ReadOnlySpan<char> entry)
        {
            var start = 0;

            for (var index = 0; index <= entry.Length; index++)
            {
                if (index != entry.Length && entry[index] is not ('/' or '\\'))
                {
                    continue;
                }

                var segment = entry[start..index];

                if (segment is "." or "..")
                {
                    return true;
                }

                start = index + 1;
            }

            return false;
        }
    }
}
