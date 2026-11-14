using DlnaServer.Host.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// What a television-traffic block refuses, and what it must leave answering.
    /// </summary>
    /// <remarks>
    /// The container health check asks <c>/health/ready</c>. Refusing it made a block report the container
    /// unhealthy for its whole 1-72 hours, so the health endpoints pass while everything a television uses
    /// still gets the 503.
    /// </remarks>
    [TestFixture]
    internal sealed class ApiBlockingMiddlewareTest
    {
        private MutableTimeProvider _time = null!;
        private ApiBlocker _blocker = null!;
        private bool _isNextCalled;
        private ApiBlockingMiddleware _middleware = null!;

        [SetUp]
        public void SetUp()
        {
            _time = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
            _blocker = new ApiBlocker(_time);
            _isNextCalled = false;
            _middleware = new ApiBlockingMiddleware(
                next: _ =>
                {
                    _isNextCalled = true;
                    return Task.CompletedTask;
                },
                blocker: _blocker,
                timeProvider: _time,
                logger: NullLogger<ApiBlockingMiddleware>.Instance);
        }

        [TestCase("/health")]
        [TestCase("/health/ready")]
        [TestCase("/admin/health")]
        public async Task InvokeAsync_WhileBlocked_PassesTheHealthEndpoints(string path)
        {
            // Arrange
            _blocker.Block(TimeSpan.FromHours(1), reason: "maintenance");
            var context = new DefaultHttpContext();
            context.Request.Path = path;

            // Act
            await _middleware.InvokeAsync(context: context);

            // Assert
            _isNextCalled.Should().BeTrue(
                "because a health check answering 503 marks the container unhealthy for the whole block");
            context.Response.StatusCode.Should().Be(StatusCodes.Status200OK,
                "because the block must not write its 503 onto a health endpoint");
        }

        [TestCase("/MediaItem/0b1c2d3e-0000-0000-0000-000000000001")]
        [TestCase("/")]
        [TestCase("/healthy")]
        public async Task InvokeAsync_WhileBlocked_RefusesTelevisionPaths(string path)
        {
            // Arrange
            _blocker.Block(TimeSpan.FromHours(1), reason: "maintenance");
            var context = new DefaultHttpContext();
            context.Request.Path = path;

            // Act
            await _middleware.InvokeAsync(context: context);

            // Assert
            _isNextCalled.Should().BeFalse(
                "because a blocked server answers a television itself, without reaching the endpoint");
            context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable,
                "because 503 is what tells a renderer to come back later - and the health exemption matches "
                + "whole path segments only, so a path merely starting with 'health' is still refused");
        }
    }
}
