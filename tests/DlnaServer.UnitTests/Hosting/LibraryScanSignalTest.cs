using DlnaServer.Core.Hosting;

namespace DlnaServer.UnitTests.Hosting
{
    /// <summary>
    /// Covers the seam the admin UI starts a scan through.
    /// </summary>
    [TestFixture]
    internal sealed class LibraryScanSignalTest
    {
        [Test]
        public async Task RequestScan_ReleasesAWaiter()
        {
            // Arrange
            using var signal = new LibraryScanSignal();

            // Act
            signal.RequestScan();

            // Assert
            await signal.WaitForRequestAsync(CancellationToken.None);
        }

        /// <summary>
        /// Repeated clicks must produce one pass, not one pass each.
        /// </summary>
        /// <remarks>
        /// A pass serialises against every other pass through a process-wide gate, so a queue of them
        /// would make an impatient operator wait through the lot - and every pass after the first would
        /// find nothing to do.
        /// </remarks>
        [Test]
        public async Task RequestScan_CalledRepeatedly_CollapsesIntoOnePass()
        {
            // Arrange
            using var signal = new LibraryScanSignal();

            // Act
            signal.RequestScan();
            signal.RequestScan();
            signal.RequestScan();

            await signal.WaitForRequestAsync(CancellationToken.None);
            var secondWait = signal.WaitForRequestAsync(CancellationToken.None);

            // Assert
            secondWait.IsCompleted.Should().BeFalse(
                "because three requests with none consumed mean one pending pass, not three");
        }

        [Test]
        public void WaitForRequestAsync_WithNothingRequested_DoesNotComplete()
        {
            // Arrange
            using var signal = new LibraryScanSignal();

            // Act
            var waiting = signal.WaitForRequestAsync(CancellationToken.None);

            // Assert
            waiting.IsCompleted.Should().BeFalse(
                "because the hosted service must sit idle until a scan is actually asked for");
        }

        /// <summary>
        /// The timed wait is what <c>Library.UsePeriodicRescan</c> runs on, and the return value is the
        /// only thing distinguishing "somebody asked" from "the interval elapsed".
        /// </summary>
        [Test]
        public async Task WaitForRequestAsync_WithATimeout_ReturnsFalseWhenNothingWasRequested()
        {
            // Arrange
            using var signal = new LibraryScanSignal();

            // Act
            var requested = await signal.WaitForRequestAsync(
                TimeSpan.FromMilliseconds(20),
                CancellationToken.None);

            // Assert
            requested.Should().BeFalse(
                "because a timed-out wait is what tells the indexer to run a pass nobody asked for");
        }

        [Test]
        public async Task WaitForRequestAsync_WithATimeout_ConsumesAPendingRequest()
        {
            // Arrange
            using var signal = new LibraryScanSignal();
            signal.RequestScan();

            // Act
            var requested = await signal.WaitForRequestAsync(
                TimeSpan.FromSeconds(30),
                CancellationToken.None);

            var second = signal.WaitForRequestAsync(CancellationToken.None);

            // Assert
            requested.Should().BeTrue(
                "because the request was pending and the wait must return immediately rather than sit "
                + "out the whole interval");
            second.IsCompleted.Should().BeFalse(
                "because the timed wait consumes the request exactly as the untimed one does");
        }

        /// <summary>
        /// A pass consumes its request, so the next one has to be asked for again - otherwise the hosted
        /// service would spin, rescanning the library without end.
        /// </summary>
        [Test]
        public async Task WaitForRequestAsync_ConsumesTheRequest()
        {
            // Arrange
            using var signal = new LibraryScanSignal();
            signal.RequestScan();

            // Act
            await signal.WaitForRequestAsync(CancellationToken.None);
            signal.RequestScan();
            await signal.WaitForRequestAsync(CancellationToken.None);
            var third = signal.WaitForRequestAsync(CancellationToken.None);

            // Assert
            third.IsCompleted.Should().BeFalse(
                "because two requests have been made and both consumed");
        }
    }
}
