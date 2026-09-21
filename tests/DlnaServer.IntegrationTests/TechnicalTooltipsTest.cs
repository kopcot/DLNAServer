using DlnaServer.Host.Diagnostics;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the period the admin pages label their values for.
    /// </summary>
    /// <remarks>
    /// It lapses on its own rather than being switched off, which is the safe direction: nothing has to
    /// remember to turn it off, and a restart turns it off anyway because the state is not persisted.
    /// </remarks>
    [TestFixture]
    internal sealed class TechnicalTooltipsTest
    {
        [Test]
        public void IsEnabled_BeforeAnythingIsAsked_IsFalse()
        {
            // Arrange
            var tooltips = new TechnicalTooltips(new MutableTimeProvider(DateTimeOffset.UnixEpoch));

            // Act
            var enabled = tooltips.IsEnabled;

            // Assert
            enabled.Should().BeFalse(
                "because labelling every value underlines a large part of every page, so it is something "
                + "the operator turns on while looking into something and not a default");
        }

        [Test]
        public void EnableFor_WithinThePeriod_IsEnabled()
        {
            // Arrange
            var clock = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
            var tooltips = new TechnicalTooltips(clock);

            // Act
            tooltips.EnableFor(TimeSpan.FromMinutes(15));
            clock.Advance(TimeSpan.FromMinutes(14));

            // Assert
            tooltips.IsEnabled.Should().BeTrue("because the period asked for has not run out yet");
            tooltips.EnabledUntilUtc.Should().Be(DateTimeOffset.UnixEpoch.AddMinutes(15),
                "because the page shows the operator when the labels will stop appearing");
        }

        [Test]
        public void IsEnabled_OnceThePeriodHasRunOut_IsFalse()
        {
            // Arrange
            var clock = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
            var tooltips = new TechnicalTooltips(clock);

            tooltips.EnableFor(TimeSpan.FromMinutes(15));

            // Act
            clock.Advance(TimeSpan.FromMinutes(16));

            // Assert
            tooltips.IsEnabled.Should().BeFalse(
                "because the period is cleared when it is next read rather than by a timer - nothing is "
                + "scheduled, so there is nothing to dispose or to leak");
            tooltips.EnabledUntilUtc.Should().BeNull("because there is no period left to report");
        }

        [Test]
        public void Disable_WhileThePeriodIsRunning_EndsItImmediately()
        {
            // Arrange
            var tooltips = new TechnicalTooltips(new MutableTimeProvider(DateTimeOffset.UnixEpoch));

            tooltips.EnableFor(TimeSpan.FromHours(1));

            // Act
            tooltips.Disable();

            // Assert
            tooltips.IsEnabled.Should().BeFalse(
                "because the operator who turned them on has to be able to turn them off again without "
                + "waiting out the period they chose");
        }

        [Test]
        public void EnableFor_WithNoDuration_IsRefused()
        {
            // Arrange
            var tooltips = new TechnicalTooltips(new MutableTimeProvider(DateTimeOffset.UnixEpoch));

            // Act
            var enabling = () => tooltips.EnableFor(TimeSpan.Zero);

            // Assert
            enabling.Should().Throw<ArgumentOutOfRangeException>(
                "because a period of zero would read as on and behave as off, which is worse than being "
                + "told the value is wrong");
        }
    }
}
