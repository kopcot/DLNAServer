using DlnaServer.Core.Hosting;
using DlnaServer.Host.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DlnaServer.IntegrationTests
{
    [TestFixture]
    internal sealed class DatabaseReadyMiddlewareTest
    {
        /// <summary>
        /// Every request after startup passes through here, so the ready case must not build a linked
        /// token source and a timer only to await a task that has already completed.
        /// </summary>
        [Test]
        public async Task InvokeAsync_WhenTheDatabaseIsAlreadyReady_PassesOnWithoutWaiting()
        {
            // Arrange
            var signal = new RecordingReadySignal(isReady: true);
            var isNextCalled = false;
            var middleware = new DatabaseReadyMiddleware(
                next: _ =>
                {
                    isNextCalled = true;
                    return Task.CompletedTask;
                },
                readySignal: signal);

            // Act
            await middleware.InvokeAsync(context: new DefaultHttpContext());

            // Assert
            isNextCalled.Should().BeTrue("because a ready database lets every request through");
            signal.WaitCount.Should().Be(0,
                "because once the schema is there the wait, and the token source it needs, are pure overhead");
        }

        [Test]
        public async Task InvokeAsync_WhenTheDatabaseBecomesReadyWhileWaiting_PassesOn()
        {
            // Arrange
            var signal = new RecordingReadySignal(isReady: false);
            var isNextCalled = false;
            var middleware = new DatabaseReadyMiddleware(
                next: _ =>
                {
                    isNextCalled = true;
                    return Task.CompletedTask;
                },
                readySignal: signal);

            // Act
            await middleware.InvokeAsync(context: new DefaultHttpContext());

            // Assert
            signal.WaitCount.Should().Be(1, "because a database that is not ready yet is waited for");
            isNextCalled.Should().BeTrue("because the request proceeds once the wait completes");
        }

        private sealed class RecordingReadySignal : IDatabaseReadySignal
        {
            public RecordingReadySignal(bool isReady)
            {
                IsReady = isReady;
            }

            public bool IsReady { get; }

            public int WaitCount { get; private set; }

            public Task WaitAsync(CancellationToken cancellationToken = default)
            {
                WaitCount++;

                return Task.CompletedTask;
            }

            public void MarkReady()
            {
            }
        }
    }
}
