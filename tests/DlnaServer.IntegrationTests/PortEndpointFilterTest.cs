using System.Net;
using DlnaServer.Host;
using DlnaServer.Host.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Proves the port guards actually refuse a request, rather than reading as though they do.
    /// </summary>
    /// <remarks>
    /// This project's own hard-won rule is that a guard must be observed refusing a real request: two
    /// have already shipped here that compiled cleanly and did nothing - an endpoint filter applied to
    /// <c>MapRazorComponents</c>, which Razor component endpoints never invoke, and
    /// <c>UseAntiforgery()</c> placed before <c>UseRouting()</c>. Nothing tested any of the three guards,
    /// so the current ones rested on exactly the same evidence as the two that failed.
    /// <para>
    /// These are unit tests over the filters and the middleware themselves, driven by a
    /// <see cref="DefaultHttpContext"/>. They cannot prove the <c>Program.cs</c> wiring, which needs a
    /// real host this project deliberately does not spin up - that gap is real and is knowingly accepted,
    /// but it is now the only part left unproven rather than the whole mechanism.
    /// </para>
    /// </remarks>
    [TestFixture]
    internal sealed class PortEndpointFilterTest
    {
        private const int MediaPort = 26_852;
        private const int AdminPort = 26_853;

        [Test]
        public async Task RequirePortEndpointFilter_OnTheWrongPort_Returns404()
        {
            // Arrange
            var filter = new RequirePortEndpointFilter(MediaPort);
            var context = CreateContext("/health", AdminPort);
            var invoked = false;

            // Act
            var result = await filter.InvokeAsync(
                new DefaultEndpointFilterInvocationContext(context),
                _ =>
                {
                    invoked = true;
                    return ValueTask.FromResult<object?>(Results.Ok());
                });

            // Assert
            invoked.Should().BeFalse(
                "because a guard that lets the request through and then rewrites the response is not a guard");
            result.Should().BeAssignableTo<IResult>(
                "because the filter answers with a result of its own rather than calling the endpoint");
            result!.GetType().Name.Should().Contain("NotFound",
                "because a surface that does not exist on this port must look absent, not forbidden");
        }

        [Test]
        public async Task RequirePortEndpointFilter_OnTheRightPort_CallsNext()
        {
            // Arrange
            var filter = new RequirePortEndpointFilter(MediaPort);
            var context = CreateContext("/health", MediaPort);
            var invoked = false;

            // Act
            _ = await filter.InvokeAsync(
                new DefaultEndpointFilterInvocationContext(context),
                _ =>
                {
                    invoked = true;
                    return ValueTask.FromResult<object?>(Results.Ok());
                });

            // Assert
            invoked.Should().BeTrue("because the endpoint belongs to this port");
        }

        [Test]
        public async Task AdminOrMediaPortEndpointFilter_ServesAdminPathsOnlyOnTheAdminPort()
        {
            // Arrange
            var filter = new AdminOrMediaPortEndpointFilter(MediaPort, AdminPort);

            // Act
            var onMediaPort = await InvokeAsync(filter, "/admin/media/thumbnail/x", MediaPort);
            var onAdminPort = await InvokeAsync(filter, "/admin/media/thumbnail/x", AdminPort);

            // Assert
            onMediaPort.Invoked.Should().BeFalse("because a renderer must not reach the admin surface");
            onAdminPort.Invoked.Should().BeTrue("because that is the port the admin surface belongs to");
        }

        [Test]
        public async Task AdminOrMediaPortEndpointFilter_ServesEverythingElseOnlyOnTheMediaPort()
        {
            // Arrange
            var filter = new AdminOrMediaPortEndpointFilter(MediaPort, AdminPort);

            // Act
            var onMediaPort = await InvokeAsync(filter, "/manage/memory", MediaPort);
            var onAdminPort = await InvokeAsync(filter, "/manage/memory", AdminPort);

            // Assert
            onMediaPort.Invoked.Should().BeTrue(
                "because /manage is not under /admin, so the filter classifies it as a media-port path");
            onAdminPort.Invoked.Should().BeFalse(
                "because a path is served on exactly one of the two ports");
        }

        /// <summary>
        /// The SOAP endpoints cannot carry an endpoint filter, so the middleware has to confine them.
        /// </summary>
        /// <remarks>
        /// <c>UseSoapEndpoint</c> is old-style middleware on <c>IApplicationBuilder</c>, so
        /// <c>AddEndpointFilter</c> structurally cannot reach it - which is why all four control endpoints
        /// answered on the admin port too.
        /// </remarks>
        [Test]
        public async Task AdminSurfaceMiddleware_ConfinesTheSoapEndpointsToTheMediaPort()
        {
            // Arrange
            var invoked = false;
            var middleware = new AdminSurfaceMiddleware(
                _ =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                },
                AdminPort);

            var context = CreateContext("/ContentDirectoryService.asmx", AdminPort);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            invoked.Should().BeFalse("because the control endpoints belong to the media port alone");
            context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound,
                "because the surface does not exist on the operator's port");
        }

        [Test]
        public async Task AdminSurfaceMiddleware_StillServesTheSoapEndpointsOnTheMediaPort()
        {
            // Arrange
            var invoked = false;
            var middleware = new AdminSurfaceMiddleware(
                _ =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                },
                AdminPort);

            var context = CreateContext("/ContentDirectoryService.asmx", MediaPort);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            invoked.Should().BeTrue("because this is the port every renderer talks to");
        }

        /// <summary>
        /// The Blazor framework paths must be refused on the media port too.
        /// </summary>
        /// <remarks>
        /// Case-insensitively and on the decoded path, because otherwise <c>/_BLAZOR/negotiate</c> or
        /// <c>/%61dmin/settings</c> would walk straight past the guard.
        /// </remarks>
        [TestCase("/admin/settings")]
        [TestCase("/_blazor/negotiate")]
        [TestCase("/_BLAZOR/negotiate")]
        [TestCase("/_framework/blazor.web.js")]
        [TestCase("/_content/DlnaServer.Admin/admin.css")]
        public async Task AdminSurfaceMiddleware_RefusesTheAdminSurfaceOnTheMediaPort(string path)
        {
            // Arrange
            var invoked = false;
            var middleware = new AdminSurfaceMiddleware(
                _ =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                },
                AdminPort);

            var context = CreateContext(path, MediaPort);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            invoked.Should().BeFalse($"because '{path}' is part of the admin surface");
            context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound,
                "because a television must not learn that an admin interface exists");
        }

        /// <summary>
        /// The admin port's root is the URL an operator actually types, and it used to answer 404.
        /// </summary>
        /// <remarks>
        /// Both halves of the confinement key on the path as well as the port, so <c>/</c> was classified
        /// as media content and refused on the very port whose only purpose is the admin UI.
        /// </remarks>
        [Test]
        public async Task AdminSurfaceMiddleware_RedirectsTheAdminPortRootToTheDashboard()
        {
            // Arrange
            var invoked = false;
            var middleware = new AdminSurfaceMiddleware(
                _ =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                },
                AdminPort);

            var context = CreateContext("/", AdminPort);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            invoked.Should().BeFalse(
                "because the redirect answers here rather than falling through to a route that 404s");
            context.Response.StatusCode.Should().Be(StatusCodes.Status302Found,
                "because a permanent redirect is cached by the browser until its cache is cleared and "
                + "could not be taken back");
            context.Response.Headers.Location.ToString().Should().Be("/admin",
                "because that is where the dashboard is, and it stays the one canonical prefix every "
                + "guard keys on");
        }

        /// <summary>
        /// The media port's root must keep serving <c>description.xml</c>.
        /// </summary>
        /// <remarks>
        /// Some renderers fetch <c>/</c> instead of the LOCATION that SSDP advertises, so redirecting it
        /// on both ports would break discovery on exactly those devices - silently, since a renderer that
        /// cannot read the description simply never appears.
        /// </remarks>
        [Test]
        public async Task AdminSurfaceMiddleware_LeavesTheMediaPortRootAlone()
        {
            // Arrange
            var invoked = false;
            var middleware = new AdminSurfaceMiddleware(
                _ =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                },
                AdminPort);

            var context = CreateContext("/", MediaPort);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            invoked.Should().BeTrue(
                "because the media port's root is description.xml, which some renderers fetch instead "
                + "of the advertised LOCATION");
            context.Response.StatusCode.Should().Be(StatusCodes.Status200OK,
                "because nothing here has answered the request - the status is still the untouched default");
        }

        /// <summary>
        /// Kestrel binds the admin port on IPv6 too, so a NAS with a global address is reachable from the
        /// internet wherever the router lets IPv6 in.
        /// </summary>
        [TestCase("/admin/settings")]
        [TestCase("/_blazor/negotiate")]
        [TestCase("/")]
        public async Task AdminSurfaceMiddleware_RefusesTheAdminPortFromOffTheLocalNetwork(string path)
        {
            // Arrange
            var context = CreateContext(path, AdminPort);
            context.Connection.RemoteIpAddress = IPAddress.Parse("2a00:1450:4001::5");
            context.Connection.LocalIpAddress = IPAddress.Parse("2001:db8:1:2::10");

            // Act
            var invoked = await InvokeAdminSurfaceAsync(context);

            // Assert
            invoked.Should().BeFalse("because a caller on the internet must not reach the admin surface");
            context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound,
                "because from off the LAN the surface does not exist, the same answer /manage gives");
        }

        [TestCase("192.168.1.20", "192.168.1.10")]
        [TestCase("100.100.1.2", "192.168.1.10")]
        [TestCase("fe80::20", "fe80::10")]
        [TestCase("2001:db8:1:2::20", "2001:db8:1:2::10")]
        public async Task AdminSurfaceMiddleware_ServesTheAdminPortToTheLocalNetwork(string remote, string local)
        {
            // Arrange
            var context = CreateContext("/admin/settings", AdminPort);
            context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
            context.Connection.LocalIpAddress = IPAddress.Parse(local);

            // Act
            var invoked = await InvokeAdminSurfaceAsync(context);

            // Assert
            invoked.Should().BeTrue(
                "because the LAN is the admin UI's audience - including Tailscale and a browser reaching the "
                + "server over its global IPv6 address from inside the same /64");
        }

        /// <summary>
        /// The remote-admin overlay's path: Caddy on loopback, and <c>UseForwardedHeaders</c> then rewriting
        /// the remote address to the internet client Caddy has already put through its password.
        /// </summary>
        [Test]
        public async Task AdminSurfaceMiddleware_JudgesTheRecordedSocketPeerNotTheForwardedClient()
        {
            // Arrange
            var context = CreateContext("/admin/settings", AdminPort);
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            AdminSurfaceMiddleware.RecordConnectionPeer(context: context, adminPort: AdminPort);
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

            // Act
            var invoked = await InvokeAdminSurfaceAsync(context);

            // Assert
            invoked.Should().BeTrue(
                "because the socket is the proxy on loopback, which is what the local-network check is about");
        }

        /// <summary>
        /// A direct caller the forwarder did not trust keeps its own address, whatever headers it sent -
        /// so the recorded peer must win over anything that looks local afterwards.
        /// </summary>
        [Test]
        public async Task AdminSurfaceMiddleware_RefusesARemoteSocketPeerWhateverTheRemoteAddressBecame()
        {
            // Arrange
            var context = CreateContext("/admin/settings", AdminPort);
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
            AdminSurfaceMiddleware.RecordConnectionPeer(context: context, adminPort: AdminPort);
            context.Connection.RemoteIpAddress = IPAddress.Loopback;

            // Act
            var invoked = await InvokeAdminSurfaceAsync(context);

            // Assert
            invoked.Should().BeFalse("because the connection itself came from the internet");
            context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound,
                "because a remote socket peer is refused no matter what a later rewrite claims");
        }

        [Test]
        public async Task AdminSurfaceMiddleware_LeavesTheMediaPortOpenToAnyAddress()
        {
            // Arrange
            var context = CreateContext("/fileserver/file/abc", MediaPort);
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
            AdminSurfaceMiddleware.RecordConnectionPeer(context: context, adminPort: AdminPort);

            // Act
            var invoked = await InvokeAdminSurfaceAsync(context);

            // Assert
            invoked.Should().BeTrue(
                "because the local-network check is the admin port's alone; the media port's wire "
                + "behaviour is unchanged");
        }

        [Test]
        public async Task RejectRemoteManagementEndpointFilter_FromOffTheLocalNetwork_Returns404()
        {
            // Arrange
            var invoked = false;
            var context = CreateContext("/manage/stop", MediaPort);
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

            // Act
            _ = await new RejectRemoteManagementEndpointFilter().InvokeAsync(
                new DefaultEndpointFilterInvocationContext(context),
                _ =>
                {
                    invoked = true;
                    return ValueTask.FromResult<object?>(Results.Ok());
                });

            // Assert
            invoked.Should().BeFalse("because /manage is unauthenticated and the LAN is its whole audience");
            context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound,
                "because from off the LAN this surface does not exist");
        }

        private static async Task<(bool Invoked, object? Result)> InvokeAsync(
            AdminOrMediaPortEndpointFilter filter,
            string path,
            int localPort)
        {
            var invoked = false;
            var context = CreateContext(path, localPort);

            var result = await filter.InvokeAsync(
                new DefaultEndpointFilterInvocationContext(context),
                _ =>
                {
                    invoked = true;
                    return ValueTask.FromResult<object?>(Results.Ok());
                });

            return (invoked, result);
        }

        private static DefaultHttpContext CreateContext(string path, int localPort)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Connection.LocalPort = localPort;

            return context;
        }

        private static async Task<bool> InvokeAdminSurfaceAsync(DefaultHttpContext context)
        {
            var invoked = false;
            var middleware = new AdminSurfaceMiddleware(
                _ =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                },
                AdminPort);

            await middleware.InvokeAsync(context);

            return invoked;
        }
    }
}
