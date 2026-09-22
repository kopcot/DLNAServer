namespace DlnaServer.Host
{
    /// <summary>
    /// Refuses a state-changing controller request that a browser reports as cross-site.
    /// </summary>
    /// <remarks>
    /// The management endpoints are unauthenticated by design, so the only thing separating an operator's
    /// deliberate <c>POST</c> from one a hostile page tricked their browser into sending is where the
    /// request came from. <c>Sec-Fetch-Site</c> is that answer and a page cannot forge it: it is a
    /// forbidden header name, so neither <c>fetch</c> nor a form can set or remove it.
    /// <para>
    /// An absent header is allowed, which is what keeps renderers and <c>curl</c> working - they send no
    /// fetch metadata at all. <b>That allowance IS a gap, and the second check is what closes it.</b>
    /// The claim this remark used to make - that every browser still shipping sends the header, so one
    /// that does not cannot be scripted - was false: Chrome and Edge shipped it in 76, Firefox in 90, but
    /// <b>Safari only in 16.4</b>. Safari 15.x is fully scriptable and sends no fetch metadata at all,
    /// which is exactly the combination the old text called impossible.
    /// </para>
    /// <para>
    /// So <c>Origin</c> is checked when the fetch metadata is missing. Every browser back to well before
    /// Safari 15 sends <c>Origin</c> on a cross-origin <c>POST</c>, and neither a renderer nor
    /// <c>curl</c> sends one - so the compatibility the absent-header allowance exists for survives while
    /// the scriptable-old-browser path closes. A same-origin <c>Origin</c> is accepted; anything else is
    /// refused.
    /// </para>
    /// <para>
    /// <b>On this server the Origin branch is not the fallback - it is the only branch.</b> Fetch
    /// metadata is sent only to potentially-trustworthy origins, and this server is plain HTTP on a LAN
    /// address, so no browser sends <c>Sec-Fetch-Site</c> here at all. Confirmed against Chrome 153 on
    /// 2026-09-16: neither a form post nor a <c>fetch</c> to <c>http://192.168.1.100:26853</c> carried
    /// one. The Safari 15.x reasoning above therefore understates it - every browser takes that path.
    /// </para>
    /// <para>
    /// Which is why <c>Referrer-Policy</c> is <c>same-origin</c> in <c>Program.cs</c> and must stay so.
    /// A navigation-mode <c>POST</c> serializes its <c>Origin</c> as the literal <c>null</c> when the
    /// document's referrer policy is <c>no-referrer</c>, and this filter then refuses the operator's own
    /// form. <c>null</c> stays refused - a sandboxed frame and a <c>data:</c> URL send it too - so the
    /// fix belongs at the header, never here.
    /// </para>
    /// <para>
    /// This does not cover the Blazor admin pages. Endpoint filters are not applied to Razor component
    /// endpoints, and they do not need it - interactive components carry anti-forgery metadata that
    /// <c>UseAntiforgery</c> validates, and a cross-origin page cannot read the negotiate response it
    /// would need to drive the circuit.
    /// </para>
    /// </remarks>
    internal sealed class RejectCrossSiteEndpointFilter : IEndpointFilter
    {
        private const string FetchSiteHeader = "Sec-Fetch-Site";
        private const string OriginHeader = "Origin";
        private const string SameOrigin = "same-origin";

        public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            var request = context.HttpContext.Request;

            if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            {
                return next(context);
            }

            return IsAllowed(request)
                ? next(context)
                : ValueTask.FromResult<object?>(Results.StatusCode(StatusCodes.Status403Forbidden));
        }

        private static bool IsAllowed(HttpRequest request)
        {
            var site = request.Headers[FetchSiteHeader].ToString();

            if (site.Length > 0)
            {
                // The header a page cannot forge, so it settles the question on its own.
                return string.Equals(site, SameOrigin, StringComparison.Ordinal);
            }

            var origin = request.Headers[OriginHeader].ToString();

            // No fetch metadata and no Origin: a renderer or curl, which is the traffic this filter must
            // not break. An Origin without fetch metadata is an older browser - Safari 15.x is the real
            // one - and then it decides.
            return origin.Length == 0 || IsSameOrigin(origin, request);
        }

        /// <remarks>
        /// Compared against the host and scheme the request actually arrived on rather than a configured
        /// name, because this server answers on two ports and on whatever address the operator reached it
        /// by. A port difference makes the origin foreign here, which is stricter than the browser's own
        /// same-site notion and is the intent: nothing in the admin UI posts across the two ports.
        /// </remarks>
        private static bool IsSameOrigin(string origin, HttpRequest request)
        {
            return Uri.TryCreate(origin, UriKind.Absolute, out var parsed)
                && string.Equals(parsed.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    parsed.Authority,
                    request.Host.Value,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
