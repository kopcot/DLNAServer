using DlnaServer.Host.Diagnostics;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// The block is state with an expiry, not a sleeping request.
    /// </summary>
    /// <remarks>
    /// The reference awaits <c>Task.Delay(hours)</c> inside the request that raised the block, so its
    /// call hangs for the whole period and the block is lost if that connection drops. These tests pin
    /// the alternative: setting it returns immediately, and it lapses on its own.
    /// </remarks>
    [TestFixture]
    internal sealed class ApiBlockerTest
    {
        private MutableTimeProvider _time = null!;
        private ApiBlocker _blocker = null!;

        [SetUp]
        public void SetUp()
        {
            _time = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
            _blocker = new ApiBlocker(_time);
        }

        [Test]
        public void IsBlocked_WhenNothingWasRequested_IsFalse()
        {
            // Assert
            _blocker.IsBlocked.Should().BeFalse("because a server refuses nothing until asked to");
            _blocker.BlockedUntilUtc.Should().BeNull("because there is no block to expire");
        }

        [Test]
        public void Block_ThenBeforeItLapses_StillBlocks()
        {
            // Arrange
            // Act
            _blocker.Block(TimeSpan.FromHours(2), reason: "maintenance");
            _time.Advance(TimeSpan.FromHours(1));

            // Assert
            _blocker.IsBlocked.Should().BeTrue("because only half the period has passed");
            _blocker.Reason.Should().Be("maintenance", "because the reason is reported back to an operator");
        }

        /// <summary>
        /// Nothing schedules the expiry, so it has to be observed on read.
        /// </summary>
        [Test]
        public void Block_OnceThePeriodHasPassed_StopsBlockingWithoutAnyTimer()
        {
            // Arrange
            // Act
            _blocker.Block(TimeSpan.FromHours(2), reason: "maintenance");
            _time.Advance(TimeSpan.FromHours(2) + TimeSpan.FromSeconds(1));

            // Assert
            _blocker.IsBlocked.Should().BeFalse(
                "because the block lapses on its own - no timer is scheduled and nothing has to be "
                + "disposed, so a lost connection cannot leave the server blocked");
            _blocker.Reason.Should().BeNull("because a lapsed block reports nothing");
        }

        [Test]
        public void Release_WhileBlocked_LiftsItImmediately()
        {
            // Arrange
            _blocker.Block(TimeSpan.FromHours(8), reason: "maintenance");

            // Act
            _blocker.Release();

            // Assert
            _blocker.IsBlocked.Should().BeFalse(
                "because /manage stays reachable while blocked precisely so this can be called");
        }

        [Test]
        public void Block_WhenAlreadyBlocked_ReplacesThePeriod()
        {
            // Arrange
            _blocker.Block(TimeSpan.FromHours(1), reason: "first");

            // Act
            _blocker.Block(TimeSpan.FromHours(4), reason: "second");
            _time.Advance(TimeSpan.FromHours(2));

            // Assert
            _blocker.IsBlocked.Should().BeTrue("because the later block replaced the earlier, shorter one");
            _blocker.Reason.Should().Be("second", "because the most recent request is the one in force");
        }
    }
}
