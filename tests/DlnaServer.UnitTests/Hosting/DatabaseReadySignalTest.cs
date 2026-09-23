using DlnaServer.Core.Hosting;

namespace DlnaServer.UnitTests.Hosting
{
    /// <summary>
    /// Covers the non-blocking half of the signal, which the readiness endpoint reads.
    /// </summary>
    [TestFixture]
    internal sealed class DatabaseReadySignalTest
    {
        [Test]
        public void IsReady_BeforeTheSchemaIsUsable_IsFalseWithoutBlocking()
        {
            // Arrange
            var signal = new DatabaseReadySignal();

            // Act
            var isReady = signal.IsReady;

            // Assert
            isReady.Should().BeFalse(
                "because a probe has to be able to report that the schema is not up yet, and WaitAsync "
                + "would hang instead of answering");
        }

        [Test]
        public async Task IsReady_OnceMarked_IsTrueAndTheWaitIsAlreadyComplete()
        {
            // Arrange
            var signal = new DatabaseReadySignal();

            // Act
            signal.MarkReady();

            // Assert
            signal.IsReady.Should().BeTrue("because the schema has been announced usable");

            await signal.WaitAsync(CancellationToken.None);
        }

        [Test]
        public void MarkReady_CalledTwice_IsIgnoredTheSecondTime()
        {
            // Arrange
            var signal = new DatabaseReadySignal();

            // Act
            signal.MarkReady();
            var second = () => signal.MarkReady();

            // Assert
            second.Should().NotThrow(
                "because the initializer and the reset path can both reach it and neither knows about "
                + "the other");
            signal.IsReady.Should().BeTrue("because the first call already latched it");
        }
    }
}
