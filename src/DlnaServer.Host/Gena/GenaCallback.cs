namespace DlnaServer.Host.Gena
{
    /// <summary>
    /// Reads the GENA <c>CALLBACK</c> header.
    /// </summary>
    /// <remarks>
    /// The wire form is one or more absolute URLs, each wrapped in angle brackets and concatenated
    /// without a separator: <c>&lt;http://192.168.1.5:9000/notify&gt;&lt;http://…&gt;</c>. The reference
    /// stores the header value verbatim, brackets included, which is not a URL anything can post to -
    /// latent only because it never sends a NOTIFY.
    /// </remarks>
    internal static class GenaCallback
    {
        /// <summary>
        /// The callback URLs in a header value, in the order the renderer listed them.
        /// </summary>
        /// <remarks>
        /// An empty result means the header is unusable and the subscription must be refused. Only
        /// absolute HTTP URLs are accepted: a relative or non-HTTP callback could not be delivered to,
        /// and accepting it would mean confirming a subscription that can never work.
        /// </remarks>
        public static IReadOnlyList<string> Parse(string? header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return [];
            }

            var urls = new List<string>(1);
            var remaining = header.AsSpan();

            while (true)
            {
                var start = remaining.IndexOf('<');

                if (start < 0)
                {
                    break;
                }

                remaining = remaining[(start + 1)..];

                var end = remaining.IndexOf('>');

                if (end < 0)
                {
                    break;
                }

                var candidate = remaining[..end].Trim();
                remaining = remaining[(end + 1)..];

                if (candidate.IsEmpty)
                {
                    continue;
                }

                var text = candidate.ToString();

                if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    urls.Add(text);
                }
            }

            return urls;
        }
    }
}
