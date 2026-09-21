using System.Globalization;

namespace DlnaServer.Host.Gena
{
    /// <summary>
    /// Reads and writes the GENA <c>TIMEOUT</c> header.
    /// </summary>
    /// <remarks>
    /// The wire form is <c>Second-1800</c> or <c>Second-infinite</c>. The reference sends
    /// <c>TimeSpan.ToString()</c> instead, which renders as <c>00:30:00</c> - not a value the header's
    /// grammar allows, so a renderer that parses it strictly gets nothing usable.
    /// </remarks>
    internal static class GenaTimeout
    {
        /// <summary>
        /// Longest subscription this server grants. The specification's recommended default, and an upper
        /// bound worth keeping: a subscription costs memory until it expires, and nothing here pushes
        /// events, so a renderer gains nothing from a longer one.
        /// </summary>
        public static readonly TimeSpan Maximum = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Shortest subscription this server grants, so a renderer asking for seconds does not renew in a
        /// tight loop.
        /// </summary>
        public static readonly TimeSpan Minimum = TimeSpan.FromMinutes(1);

        private const string SecondPrefix = "Second-";

        /// <summary>
        /// The duration to grant for a requested <c>TIMEOUT</c> value.
        /// </summary>
        /// <remarks>
        /// A server may grant less than was asked for, so anything absent, infinite or unparseable
        /// resolves to <see cref="Maximum"/> rather than being refused.
        /// </remarks>
        public static TimeSpan Grant(string? requested)
        {
            if (string.IsNullOrWhiteSpace(requested))
            {
                return Maximum;
            }

            var value = requested.AsSpan().Trim();

            if (!value.StartsWith(SecondPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return Maximum;
            }

            var seconds = value[SecondPrefix.Length..];

            if (!int.TryParse(seconds, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                // Covers "infinite", which is legal, and anything malformed, which is not.
                return Maximum;
            }

            var clamped = TimeSpan.FromSeconds(parsed);

            return clamped < Minimum
                ? Minimum
                : clamped > Maximum
                    ? Maximum
                    : clamped;
        }

        /// <summary>
        /// Formats a granted duration for the <c>TIMEOUT</c> response header.
        /// </summary>
        public static string Format(TimeSpan granted)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{SecondPrefix}{(int)granted.TotalSeconds}");
        }
    }
}
