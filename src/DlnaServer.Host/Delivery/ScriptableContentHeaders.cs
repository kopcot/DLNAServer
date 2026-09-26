using DlnaServer.Core.Dlna;
using Microsoft.Net.Http.Headers;

namespace DlnaServer.Host.Delivery
{
    /// <summary>
    /// Confines a media response whose type can carry script, so opening it directly runs nothing.
    /// </summary>
    /// <remarks>
    /// SVG and TTML are the two such types the catalog knows: SVG is served inline on the admin origin as
    /// well as the media port, and TTML is an XML subtitle the validator already refuses to link, sandboxed
    /// here as well in case one is ever served. The application-wide policy is otherwise the only barrier
    /// between a file someone dropped on the media share and script running on the unauthenticated admin
    /// origin; this is a second one, on the response itself.
    /// <para>
    /// <b>Appended, not assigned.</b> The global header middleware has already set the site policy by the
    /// time a controller runs, and a response carrying two <c>Content-Security-Policy</c> headers is held
    /// to both - so <c>sandbox</c> is added on top of the site policy rather than replacing it.
    /// </para>
    /// </remarks>
    internal static class ScriptableContentHeaders
    {
        /// <summary>
        /// <b>Content-Security-Policy: sandbox</b><br />
        /// Treats the document as a unique origin with scripts, forms and plugins disabled.
        /// </summary>
        private const string SandboxPolicy = "sandbox";

        public static void Apply(HttpResponse response, DlnaMime mime)
        {
            if (mime is DlnaMime.ImageSvgXml or DlnaMime.SubtitleTtmlXml)
            {
                response.Headers.Append(HeaderNames.ContentSecurityPolicy, SandboxPolicy);
            }
        }
    }
}
