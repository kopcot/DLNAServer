using System.Buffers;

namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// The one form a configured file extension is stored in, and the characters it may not contain.
    /// </summary>
    /// <remarks>
    /// Shared by the file types and subtitle types editors, the defaults that normalise what was bound and
    /// the validator that refuses what cannot be used, so the four cannot disagree about what an extension is.
    /// </remarks>
    public static class FileExtension
    {
        // Characters an extension may not contain past its leading dot.
        private static readonly SearchValues<char> _rejected = SearchValues.Create(" \t\r\n/\\:*?\"<>|");

        /// <summary>
        /// An extension as the configuration stores it - trimmed, lower case, with a leading dot. Empty when
        /// nothing is left.
        /// </summary>
        public static string Normalise(string? extension)
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

        /// <summary>
        /// Whether the text contains a character no extension may hold - a space, a slash, or anything a
        /// file name cannot carry.
        /// </summary>
        /// <param name="afterDot">The extension without its leading dot.</param>
        public static bool HasRejectedCharacter(ReadOnlySpan<char> afterDot)
        {
            return afterDot.ContainsAny(_rejected);
        }
    }
}
