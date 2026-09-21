using Microsoft.Net.Http.Headers;

namespace DlnaServer.Host.Uploads
{
    /// <summary>
    /// Reads the boundary a multipart body is divided by out of its content type.
    /// </summary>
    internal static class MultipartRequestBoundary
    {
        /// <summary>
        /// The boundary is chosen by the browser, so it is length-limited before it is used: it names
        /// nothing and is only a separator, and a very long one is a sign of something other than a form.
        /// </summary>
        private const int LengthLimit = 128;

        public static bool TryRead(string? contentType, out string boundary)
        {
            boundary = string.Empty;

            if (string.IsNullOrEmpty(contentType)
                || !contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase)
                || !MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
            {
                return false;
            }

            var value = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;

            if (string.IsNullOrWhiteSpace(value) || value.Length > LengthLimit)
            {
                return false;
            }

            boundary = value;

            return true;
        }
    }
}
