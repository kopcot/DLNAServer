using DlnaServer.Core.Hosting;

namespace DlnaServer.UnitTests.Hosting
{
    /// <summary>
    /// Proves the lock actually serialises, and that it is released when the operation throws.
    /// </summary>
    /// <remarks>
    /// This replaced a private <c>static SemaphoreSlim</c> inside <c>LibraryIndexer</c>, which nothing
    /// covered. The release-on-throw case is the one worth pinning: a rebuild that fails on
    /// <c>database is locked</c> must not leave every later scan waiting forever on a permit nobody
    /// holds - and the whole point of moving the lock out was that a third caller can now take it.
    /// </remarks>
    [TestFixture]
    internal sealed class LibraryIndexLockTest
    {
        [Test]
        public async Task RunAsync_WithASecondCallerWaiting_DoesNotOverlap()
        {
            // Arrange
            using var indexLock = new LibraryIndexLock();
            var firstEntered = new TaskCompletionSource();
            var releaseFirst = new TaskCompletionSource();
            var overlapped = false;

            var first = indexLock.RunAsync(
                async _ =>
                {
                    firstEntered.SetResult();
                    await releaseFirst.Task;
                },
                CancellationToken.None);

            await firstEntered.Task;

            // Act
            var second = indexLock.RunAsync(
                _ =>
                {
                    overlapped = !first.IsCompleted;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            var overlappedWhileFirstHeld = second.IsCompleted;

            releaseFirst.SetResult();
            await Task.WhenAll(first, second);

            // Assert
            overlappedWhileFirstHeld.Should().BeFalse(
                "because a second whole-index operation must wait rather than run alongside the first");
            overlapped.Should().BeFalse(
                "because the first operation had to have finished before the second was let in");
        }

        [Test]
        public async Task RunAsync_WhenTheOperationThrows_StillReleasesTheLock()
        {
            // Arrange
            using var indexLock = new LibraryIndexLock();

            var failing = async () => await indexLock.RunAsync(
                _ => throw new InvalidOperationException("the rebuild failed"),
                CancellationToken.None);

            _ = await failing.Should().ThrowAsync<InvalidOperationException>(
                "because the caller decides what a failed index operation means, not the lock");

            // Act
            var second = indexLock.RunAsync(
                static _ => Task.CompletedTask,
                CancellationToken.None);

            // Assert
            second.IsCompleted.Should().BeTrue(
                "because a failed operation must not strand the permit and deadlock every later scan");
        }

        [Test]
        public async Task RunAsync_ReturningAValue_PassesItBack()
        {
            // Arrange
            using var indexLock = new LibraryIndexLock();

            // Act
            var result = await indexLock.RunAsync(
                static _ => Task.FromResult(42),
                CancellationToken.None);

            // Assert
            result.Should().Be(42,
                "because the indexer's own pass returns a LibraryIndexResult through this overload");
        }

        [Test]
        public async Task RunAsync_WhenAlreadyCancelled_DoesNotRunTheOperation()
        {
            // Arrange
            using var indexLock = new LibraryIndexLock();
            using var cts = new CancellationTokenSource();
            var ran = false;

            await cts.CancelAsync();

            var cancelled = async () => await indexLock.RunAsync(
                _ =>
                {
                    ran = true;
                    return Task.CompletedTask;
                },
                cts.Token);

            // Act
            _ = await cancelled.Should().ThrowAsync<OperationCanceledException>(
                "because waiting for the permit honours the caller's token");

            // Assert
            ran.Should().BeFalse(
                "because a cancelled caller must not start an index pass it cannot finish");
        }
    }
}
