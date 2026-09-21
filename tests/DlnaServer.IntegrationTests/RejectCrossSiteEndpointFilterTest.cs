using DlnaServer.Host;
using Microsoft.AspNetCore.Http;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Proves the CSRF guard refuses a real cross-site request and lets a renderer through.
    /// </summary>
    /// <remarks>
    /// The gap this closes was open because the controller's own documentation claimed the <c>POST</c>
    /// verb was the whole defence, and nothing tested the claim. Six reviewers found it independently.
    /// The two halves worth pinning are both here: a cross-site <c>POST</c> is refused, and a request
    /// carrying no fetch metadata at all is not - the second is what keeps televisions and <c>curl</c>
    /// working, and getting it wrong would make the server unreachable rather than merely unguarded.
    /// <para>
    /// Like <see cref="PortEndpointFilterTest"/>, this drives the filter over a
    /// <see cref="DefaultHttpContext"/> and so cannot prove the <c>Program.cs</c> wiring.
    /// </para>
    /// </remarks>
    [TestFixture]
    internal sealed class RejectCrossSiteEndpointFilterTest
    {
        private const string FetchSiteHeader = "Sec-Fetch-Site";
        private const string OriginHeader = "Origin";

        [TestCase("cross-site")]
        [TestCase("same-site")]
        public async Task Post_ReportedAsCrossSite_Is403(string site)
        {
            // Arrange
            var filter = new RejectCrossSiteEndpointFilter();

            // Act
            var (invoked, result) = await InvokeAsync(filter, HttpMethods.Post, site);

            // Assert
            invoked.Should().BeFalse(
                "because a request the browser reports as coming from another site must never reach a mutating endpoint");
            result!.GetType().Name.Should().Contain("StatusCode",
                "because the filter answers with a status of its own rather than calling the endpoint");
        }

        [Test]
        public async Task Post_ReportedAsSameOrigin_CallsNext()
        {
            // Arrange
            var filter = new RejectCrossSiteEndpointFilter();

            // Act
            var (invoked, _) = await InvokeAsync(filter, HttpMethods.Post, site: "same-origin");

            // Assert
            invoked.Should().BeTrue("because that is the operator's own browser on the server's own page");
        }

        /// <summary>
        /// A renderer, <c>curl</c> and every non-browser client send no fetch metadata at all.
        /// </summary>
        /// <remarks>
        /// This is the case that decides whether the guard is deployable. Refusing an absent header would
        /// close <c>/manage</c> to the operator's own shell and to any renderer that ever posts, which is
        /// a far larger break than the gap being fixed. The header itself is a forbidden header name, so
        /// a page cannot remove it - but "a browser old enough not to send it is too old to be scripted"
        /// was false, and Safari 15.x is the counter-example. That gap is closed by the Origin check,
        /// pinned below, not by this allowance.
        /// </remarks>
        [Test]
        public async Task Post_WithNoFetchMetadata_CallsNext()
        {
            // Arrange
            var filter = new RejectCrossSiteEndpointFilter();

            // Act
            var (invoked, _) = await InvokeAsync(filter, HttpMethods.Post, site: null);

            // Assert
            invoked.Should().BeTrue(
                "because a client that sends no Sec-Fetch-Site header is not a browser and must keep working");
        }

        /// <summary>
        /// A browser that sends no fetch metadata but does send a foreign <c>Origin</c> is refused.
        /// </summary>
        /// <remarks>
        /// <b>Safari 15.x is this case</b>, and the filter's own remark used to assert it could not
        /// exist: Safari shipped <c>Sec-Fetch-*</c> only in 16.4, while 15.x is fully scriptable. The
        /// reachable exploit was an auto-posting form to <c>/manage/clearAllMetadata</c>, which discards
        /// every stored detail and preview in a 25,000-file library.
        /// </remarks>
        [TestCase("http://evil.example")]
        [TestCase("http://nas:26853")]
        [TestCase("https://nas:26852")]
        public async Task Post_WithNoFetchMetadataButAForeignOrigin_Is403(string origin)
        {
            // Arrange
            var filter = new RejectCrossSiteEndpointFilter();

            // Act
            var (invoked, _) = await InvokeAsync(filter, HttpMethods.Post, site: null, origin: origin);

            // Assert
            invoked.Should().BeFalse(
                $"because '{origin}' is not the origin this request arrived on, and an older browser "
                + "that sends no fetch metadata still sends Origin on a cross-origin POST");
        }

        /// <summary>
        /// An opaque origin stays refused, and the fix for it is not here.
        /// </summary>
        /// <remarks>
        /// <c>null</c> is what a sandboxed frame and a <c>data:</c> URL send, so allowing it would hand
        /// back the gap the Origin check exists to close. It is also what the operator's own upload form
        /// sent while <c>Program.cs</c> answered <c>Referrer-Policy: no-referrer</c> - a navigation-mode
        /// <c>POST</c> serializes its Origin as <c>null</c> under that policy, and since no browser sends
        /// fetch metadata to a plain-HTTP origin, this branch decided every admin form post and refused
        /// them all. The header is <c>same-origin</c> now; this test is what stops the 403 being "fixed"
        /// by relaxing the filter instead.
        /// </remarks>
        [Test]
        public async Task Post_WithAnOpaqueOrigin_Is403()
        {
            // Arrange
            var filter = new RejectCrossSiteEndpointFilter();

            // Act
            var (invoked, _) = await InvokeAsync(filter, HttpMethods.Post, site: null, origin: "null");

            // Assert
            invoked.Should().BeFalse(
                "because an opaque origin names no site that could be this one, and a sandboxed frame "
                + "posts with exactly that value");
        }

        /// <summary>
        /// The same older browser on the server's own page is allowed.
        /// </summary>
        [Test]
        public async Task Post_WithNoFetchMetadataAndItsOwnOrigin_CallsNext()
        {
            // Arrange
            var filter = new RejectCrossSiteEndpointFilter();

            // Act
            var (invoked, _) = await InvokeAsync(
                filter,
                HttpMethods.Post,
                site: null,
                origin: "http://nas:26852");

            // Assert
            invoked.Should().BeTrue(
                "because that is the operator's own browser on the port the request arrived on - "
                + "refusing it would break the admin UI on every pre-16.4 Safari");
        }

        [TestCase("GET")]
        [TestCase("HEAD")]
        public async Task SafeMethod_EvenCrossSite_CallsNext(string method)
        {
            // Arrange
            var filter = new RejectCrossSiteEndpointFilter();

            // Act
            var (invoked, _) = await InvokeAsync(filter, method, site: "cross-site");

            // Assert
            invoked.Should().BeTrue(
                "because every media and thumbnail request a renderer makes is a GET or a HEAD, "
                + "and reading changes nothing");
        }

        private static async Task<(bool Invoked, object? Result)> InvokeAsync(
            RejectCrossSiteEndpointFilter filter,
            string method,
            string? site,
            string? origin = null)
        {
            var invoked = false;
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = "/manage/stop";
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("nas", 26852);

            if (site is not null)
            {
                context.Request.Headers[FetchSiteHeader] = site;
            }

            if (origin is not null)
            {
                context.Request.Headers[OriginHeader] = origin;
            }

            var result = await filter.InvokeAsync(
                new DefaultEndpointFilterInvocationContext(context),
                _ =>
                {
                    invoked = true;
                    return ValueTask.FromResult<object?>(Results.Ok());
                });

            return (invoked, result);
        }
    }
}
