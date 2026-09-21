using DlnaServer.Core.Hosting;

namespace DlnaServer.UnitTests.Hosting
{
    /// <summary>
    /// Covers the request that carries a database rebuild across a host restart.
    /// </summary>
    /// <remarks>
    /// Small on purpose, and worth having anyway: the whole safety argument for deleting a database file
    /// only at startup rests on this request surviving the container it was raised in and being consumed
    /// exactly once.
    /// </remarks>
    [TestFixture]
    internal sealed class DatabaseResetSignalTest
    {
        [Test]
        public void IsResetRequested_OnAFreshSignal_IsFalse()
        {
            // Assert
            new DatabaseResetSignal().IsResetRequested.Should().BeFalse(
                "because an ordinary start must never discard the database");
        }

        [Test]
        public void RequestReset_RaisesTheRequest()
        {
            // Arrange
            var signal = new DatabaseResetSignal();

            // Act
            signal.RequestReset();

            // Assert
            signal.IsResetRequested.Should().BeTrue(
                "because the next startup reads this to know it must rebuild");
        }

        /// <summary>
        /// The initializer clears it after acting, and it has to stay clear - a request that survived
        /// would wipe the database on every subsequent restart.
        /// </summary>
        [Test]
        public void Reset_ClearsTheRequest()
        {
            // Arrange
            var signal = new DatabaseResetSignal();
            signal.RequestReset();

            // Act
            signal.Reset();

            // Assert
            signal.IsResetRequested.Should().BeFalse(
                "because a reset happens once, not on every restart from then on");
        }
    }
}
