using DlnaServer.Core.Configuration;
using DlnaServer.Host.Diagnostics;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the reveal window and the two lists composed from it.
    /// </summary>
    /// <remarks>
    /// The composition is deliberately not duplicated in a test double, so this is the only place it is
    /// checked - a repository test that composed its own answer could keep passing while the real rule
    /// changed underneath it.
    /// </remarks>
    [TestFixture]
    internal sealed class TemporaryFolderVisibilityTest
    {
        private const string Excluded = "@Recycle";
        private const string Hidden = "Private";

        [Test]
        public void HiddenFromListings_WhileShut_HoldsBothLists()
        {
            // Arrange
            var visibility = Create(out _);

            // Act
            var listings = visibility.HiddenFromListings;

            // Assert
            listings.Should().BeEquivalentTo([Excluded, Hidden],
                "because a listing hides the retired folders and the temporarily hidden ones alike");
        }

        [Test]
        public void HiddenFromListings_WhileOpen_HoldsOnlyTheExcludedFolders()
        {
            // Arrange
            var visibility = Create(out _);
            visibility.ShowFor(TimeSpan.FromMinutes(10));

            // Act
            var listings = visibility.HiddenFromListings;

            // Assert
            listings.Should().BeEquivalentTo([Excluded],
                "because revealing lifts the temporary half and never the permanent one - an excluded "
                + "folder is not even scanned, so showing it would list rows nothing keeps current");
        }

        [Test]
        public void HiddenFromListings_WithNothingTemporarilyHidden_ReturnsTheConfiguredListItself()
        {
            // Arrange
            var options = new DlnaOptions();
            options.Library.ExcludeFolders = [Excluded];

            var visibility = new TemporaryFolderVisibility(
                new StaticOptionsMonitor<DlnaOptions>(options),
                new MutableTimeProvider(DateTimeOffset.UnixEpoch));

            // Act
            var listings = visibility.HiddenFromListings;

            // Assert
            listings.Should().BeSameAs(options.Library.ExcludeFolders,
                "because the ordinary library must not pay a list allocation on the Browse path, which "
                + "asks this three times per request");
        }

        [Test]
        public void HiddenFromDelivery_WhileShut_HoldsOnlyTheTemporarilyHiddenFolders()
        {
            // Arrange
            var visibility = Create(out _);

            // Act
            var delivery = visibility.HiddenFromDelivery;

            // Assert
            delivery.Should().BeEquivalentTo([Hidden],
                "because delivery by identifier has always been exempt from the excluded folders, so a "
                + "renderer already streaming a retired file is not cut off");
        }

        [Test]
        public void HiddenFromDelivery_WhileOpen_HoldsNothing()
        {
            // Arrange
            var visibility = Create(out _);
            visibility.ShowFor(TimeSpan.FromMinutes(10));

            // Act
            var delivery = visibility.HiddenFromDelivery;

            // Assert
            delivery.Should().BeEmpty("because the whole point of revealing is that the files can be played");
        }

        /// <summary>
        /// The expiry is absolute and lazily observed, so nothing has to be scheduled or disposed.
        /// </summary>
        [Test]
        public void IsVisible_OnceTheWindowHasLapsed_IsFalseWithoutAnythingHavingRunInBetween()
        {
            // Arrange
            var visibility = Create(out var clock);
            visibility.ShowFor(TimeSpan.FromMinutes(5));

            // Act
            clock.Advance(TimeSpan.FromMinutes(5));

            // Assert
            visibility.IsVisible.Should().BeFalse(
                "because the window is compared against the clock when it is read, not cleared by a timer");
            visibility.VisibleUntilUtc.Should().BeNull(
                "because a lapsed window is no window at all, not one with a past expiry");
            visibility.HiddenFromListings.Should().BeEquivalentTo([Excluded, Hidden],
                "because the folders go back to being hidden the moment the window lapses");
        }

        [Test]
        public void IsVisible_JustBeforeTheWindowLapses_IsStillTrue()
        {
            // Arrange
            var visibility = Create(out var clock);
            visibility.ShowFor(TimeSpan.FromMinutes(5));

            // Act
            clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));

            // Assert
            visibility.IsVisible.Should().BeTrue(
                "because the window runs to its expiry rather than ending a tick early");
        }

        [Test]
        public void ShowFor_WhileAlreadyOpen_ReplacesTheWindowRatherThanExtendingIt()
        {
            // Arrange
            var visibility = Create(out var clock);
            var start = clock.GetUtcNow();

            visibility.ShowFor(TimeSpan.FromMinutes(60));

            // Act
            visibility.ShowFor(TimeSpan.FromMinutes(5));

            // Assert
            visibility.VisibleUntilUtc.Should().Be(start.AddMinutes(5),
                "because asking for five minutes must mean five minutes, whatever was asked for before");
        }

        [Test]
        public void Hide_WhileOpen_EndsTheWindowImmediately()
        {
            // Arrange
            var visibility = Create(out _);
            visibility.ShowFor(TimeSpan.FromMinutes(60));

            // Act
            visibility.Hide();

            // Assert
            visibility.IsVisible.Should().BeFalse("because Hide is the way out before the time is up");
            visibility.HiddenFromDelivery.Should().BeEquivalentTo([Hidden],
                "because hiding again must restore the delivery block, not only the listings");
        }

        [Test]
        public void ShowFor_WithAZeroOrNegativeDuration_IsRefused()
        {
            // Arrange
            var visibility = Create(out _);

            // Act
            var zero = () => visibility.ShowFor(TimeSpan.Zero);
            var negative = () => visibility.ShowFor(TimeSpan.FromMinutes(-1));

            // Assert
            zero.Should().Throw<ArgumentOutOfRangeException>(
                "because a window that is already over is a mistake, not a way to hide");
            negative.Should().Throw<ArgumentOutOfRangeException>(
                "because the same holds for a duration that runs backwards");
        }

        /// <remarks>
        /// The lists are read from the monitor on every access rather than cached, so that a reload the
        /// validator rejects cannot leave hiding following rules nothing else in the server agrees with.
        /// </remarks>
        [Test]
        public void HiddenFromListings_AfterTheConfigurationChanges_FollowsTheNewValue()
        {
            // Arrange
            var options = new DlnaOptions();
            options.Library.ExcludeFolders = [Excluded];
            options.Library.TemporarilyHiddenFolders = [Hidden];

            var monitor = new MutableOptionsMonitor<DlnaOptions>(options);
            var visibility = new TemporaryFolderVisibility(monitor, new MutableTimeProvider(DateTimeOffset.UnixEpoch));

            var replacement = new DlnaOptions();
            replacement.Library.ExcludeFolders = [Excluded];
            replacement.Library.TemporarilyHiddenFolders = ["Personal"];

            // Act
            monitor.CurrentValue = replacement;

            // Assert
            visibility.HiddenFromListings.Should().BeEquivalentTo([Excluded, "Personal"],
                "because the value is read when it is asked for, so an edit applies without a restart "
                + "and without a change notification this type would have to trust");
        }

        private static TemporaryFolderVisibility Create(out MutableTimeProvider clock)
        {
            var options = new DlnaOptions();
            options.Library.ExcludeFolders = [Excluded];
            options.Library.TemporarilyHiddenFolders = [Hidden];

            clock = new MutableTimeProvider(DateTimeOffset.UnixEpoch);

            return new TemporaryFolderVisibility(new StaticOptionsMonitor<DlnaOptions>(options), clock);
        }
    }
}
