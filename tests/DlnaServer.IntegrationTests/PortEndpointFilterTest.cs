using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using DlnaServer.Host;
using DlnaServer.Host.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

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
                NullLogger<AdminSurfaceMiddleware>.Instance,
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
                NullLogger<AdminSurfaceMiddleware>.Instance,
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
                NullLogger<AdminSurfaceMiddleware>.Instance,
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
                NullLogger<AdminSurfaceMiddleware>.Instance,
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
                NullLogger<AdminSurfaceMiddleware>.Instance,
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
            AdminSurfaceMiddleware.RecordConnectionPeer(context: context);
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
            AdminSurfaceMiddleware.RecordConnectionPeer(context: context);
            context.Connection.RemoteIpAddress = IPAddress.Loopback;

            // Act
            var invoked = await InvokeAdminSurfaceAsync(context);

            // Assert
            invoked.Should().BeFalse("because the connection itself came from the internet");
            context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound,
                "because a remote socket peer is refused no matter what a later rewrite claims");
        }

        /// <summary>
        /// The media port answers the local network only, like the admin port (ADR 0036).
        /// </summary>
        /// <remarks>
        /// This test used to pin the opposite - the media port open to any address. A review then listed
        /// and downloaded the whole library from a global IPv6 peer, and the maintainer chose to gate it.
        /// </remarks>
        [TestCase("POST", "/ContentDirectoryService.asmx", "203.0.113.7")]
        [TestCase("POST", "/ContentDirectoryService.asmx", "2a00:1450:4001::5")]
        [TestCase("GET", "/fileserver/file/7d3f0c4e-2b1a-4c8e-9f00-0a1b2c3d4e5f", "203.0.113.7")]
        [TestCase("GET", "/fileserver/file/7d3f0c4e-2b1a-4c8e-9f00-0a1b2c3d4e5f", "2a00:1450:4001::5")]
        [TestCase("SUBSCRIBE", "/event/eventAction/ContentDirectory", "203.0.113.7")]
        [TestCase("SUBSCRIBE", "/event/eventAction/ContentDirectory", "2a00:1450:4001::5")]
        [TestCase("GET", "/media/description.xml", "203.0.113.7")]
        [TestCase("GET", "/media/description.xml", "2a00:1450:4001::5")]
        [TestCase("GET", "/", "2a00:1450:4001::5")]
        [TestCase("GET", "/fileserver/file/7d3f0c4e-2b1a-4c8e-9f00-0a1b2c3d4e5f", "::ffff:203.0.113.7")]
        public async Task AdminSurfaceMiddleware_RefusesTheMediaPortFromOffTheLocalNetwork(
            string method,
            string path,
            string remote)
        {
            // Arrange
            var context = CreateMediaRequest(method: method, path: path, remote: remote);

            // Act
            var invoked = await InvokeAdminSurfaceAsync(context);

            // Assert
            invoked.Should().BeFalse(
                $"because a caller on the internet must not reach {method} {path} - it lists or serves the library");
            context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound,
                "because from off the LAN the media port does not exist either, the same answer /manage gives");
        }

        [Test]
        public async Task AdminSurfaceMiddleware_ServesTheMediaPortToTheLocalNetwork(
            [Values(
                "POST /ContentDirectoryService.asmx",
                "GET /fileserver/file/7d3f0c4e-2b1a-4c8e-9f00-0a1b2c3d4e5f",
                "SUBSCRIBE /event/eventAction/ContentDirectory",
                "GET /media/description.xml")]
            string request,
            [Values(
                "127.0.0.1",
                "::1",
                "192.168.1.20",
                "10.0.0.5",
                "172.16.4.4",
                "169.254.10.10",
                "100.100.1.2",
                "fe80::20",
                "fd12:3456:789a::20",
                "::ffff:192.168.1.20",
                "2001:db8:1:2::20")]
            string remote)
        {
            // Arrange
            var parts = request.Split(' ');
            var context = CreateMediaRequest(method: parts[0], path: parts[1], remote: remote);

            // Act
            var invoked = await InvokeAdminSurfaceAsync(context);

            // Assert
            invoked.Should().BeTrue(
                $"because {remote} is on the local network, where every renderer is - including a peer in "
                + "the server's own IPv6 /64");
        }

        /// <summary>
        /// The media port judges the socket's peer too, not the address <c>UseForwardedHeaders</c> wrote later.
        /// </summary>
        [Test]
        public async Task AdminSurfaceMiddleware_RefusesARemoteSocketPeerOnTheMediaPortWhateverTheRemoteAddressBecame()
        {
            // Arrange
            var context = CreateMediaRequest(method: "GET", path: "/fileserver/file/abc", remote: "203.0.113.7");
            AdminSurfaceMiddleware.RecordConnectionPeer(context: context);
            context.Connection.RemoteIpAddress = IPAddress.Loopback;

            // Act
            var invoked = await InvokeAdminSurfaceAsync(context);

            // Assert
            invoked.Should().BeFalse(
                "because the connection itself came from the internet, whatever a later rewrite claims");
            context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound,
                "because a refused peer sees the media port as absent");
        }

        /// <summary>
        /// One middleware instance serves every connection, so interleaved local and global peers must not
        /// leak a verdict from one request into another.
        /// </summary>
        [Test]
        public async Task AdminSurfaceMiddleware_ConcurrentMixedPeers_EachGetsItsOwnVerdict()
        {
            // Arrange
            const int RequestCount = 400;
            var invokedPaths = new ConcurrentBag<string>();
            var middleware = new AdminSurfaceMiddleware(
                context =>
                {
                    invokedPaths.Add(context.Request.Path.Value!);
                    return Task.CompletedTask;
                },
                NullLogger<AdminSurfaceMiddleware>.Instance,
                AdminPort);

            var contexts = new DefaultHttpContext[RequestCount];

            for (var i = 0; i < RequestCount; i++)
            {
                var remote = (i % 2) == 0 ? "192.168.1.20" : "2a00:1450:4001::5";
                contexts[i] = CreateMediaRequest(method: "GET", path: $"/fileserver/file/{i}", remote: remote);
                AdminSurfaceMiddleware.RecordConnectionPeer(context: contexts[i]);
            }

            // Act
            await Task.WhenAll(contexts.Select(c => Task.Run(() => middleware.InvokeAsync(c))));

            // Assert
            invokedPaths.Should().HaveCount(RequestCount / 2,
                "because exactly the local half of the requests may reach the endpoint");
            invokedPaths.Select(static p => int.Parse(p["/fileserver/file/".Length..]) % 2).Should().OnlyContain(
                static r => r == 0,
                "because every request that got through must be one from the local peer");
            contexts.Where(static (_, i) => (i % 2) == 1).Should().OnlyContain(
                static c => c.Response.StatusCode == StatusCodes.Status404NotFound,
                "because every request from the global peer must be refused with 404");
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

        private static DefaultHttpContext CreateMediaRequest(string method, string path, string remote)
        {
            var remoteAddress = IPAddress.Parse(remote);
            var context = CreateContext(path, MediaPort);
            context.Request.Method = method;
            context.Connection.RemoteIpAddress = remoteAddress;
            context.Connection.LocalIpAddress = remoteAddress.AddressFamily == AddressFamily.InterNetworkV6
                ? IPAddress.Parse("2001:db8:1:2::10")
                : IPAddress.Parse("192.168.1.10");

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
                NullLogger<AdminSurfaceMiddleware>.Instance,
                AdminPort);

            await middleware.InvokeAsync(context);

            return invoked;
        }
    }
}
