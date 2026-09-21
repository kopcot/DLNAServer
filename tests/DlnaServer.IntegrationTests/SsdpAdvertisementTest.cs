using System.Net;
using DlnaServer.Host.Upnp.Discovery;
using DlnaServer.Upnp.Constants;
using DlnaServer.Upnp.Ssdp;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers SSDP search-target matching, including the defect this rewrite fixes.
    /// </summary>
    [TestFixture]
    internal sealed class SsdpAdvertisementTest
    {
        private static readonly UpnpDeviceIdentity _identity = new(
            new Guid("11111111-2222-3333-4444-555555555555"),
            IPAddress.Parse("192.168.1.50"),
            26_851);

        [Test]
        public void NotificationTypesFor_AdvertisesAllSevenTargets()
        {
            // Arrange
            // Act
            var types = SsdpAdvertisement.NotificationTypesFor(_identity);

            // Assert
            types.Should().Equal(
                [
                    "upnp:rootdevice",
                    "urn:schemas-upnp-org:device:MediaServer:1",
                    "urn:schemas-upnp-org:service:ContentDirectory:1",
                    "urn:schemas-upnp-org:service:AVTransport:1",
                    "urn:schemas-upnp-org:service:ConnectionManager:1",
                    "urn:schemas-upnp-org:service:X_MS_MediaReceiverRegistrar:1",
                    $"uuid:{_identity.DeviceId}",
                ],
                "because a renderer may listen for any one of them and the reference announces all seven");
        }

        [Test]
        public void ShouldAnswer_ForSsdpAll_AnswersEveryTarget()
        {
            // Arrange
            // Act & Assert
            foreach (var notificationType in SsdpAdvertisement.NotificationTypesFor(_identity))
            {
                SsdpAdvertisement.ShouldAnswer("ssdp:all", notificationType, useLegacyInvertedMatch: false)
                    .Should().BeTrue("because ssdp:all asks for everything the device offers");
            }
        }

        /// <summary>
        /// The defect: the reference answered every type <b>except</b> the one requested, so a device
        /// doing a targeted search never received a usable reply.
        /// </summary>
        [Test]
        public void ShouldAnswer_ForATargetedSearch_AnswersOnlyThatTarget()
        {
            // Arrange
            const string requested = UpnpServices.ServiceType.ContentDirectory;

            // Act
            var matched = SsdpAdvertisement.ShouldAnswer(requested, requested, useLegacyInvertedMatch: false);
            var other = SsdpAdvertisement.ShouldAnswer(
                requested,
                UpnpServices.ServiceType.AvTransport,
                useLegacyInvertedMatch: false);

            // Assert
            matched.Should().BeTrue("because the renderer asked for exactly this service");
            other.Should().BeFalse("because answering with an unrelated service type is what the "
                + "reference did, and a strict renderer discards it");
        }

        [Test]
        public void ShouldAnswer_ForTheDeviceUuid_AnswersThatTarget()
        {
            // Arrange
            var uuidTarget = $"uuid:{_identity.DeviceId}";

            // Act
            var answered = SsdpAdvertisement.ShouldAnswer(uuidTarget, uuidTarget, useLegacyInvertedMatch: false);

            // Assert
            answered.Should().BeTrue("because a renderer may search for a device it already knows by uuid");
        }

        /// <summary>
        /// The escape hatch, in case a renderer turns out to depend on the old flood of wrong answers.
        /// </summary>
        [Test]
        public void ShouldAnswer_WithLegacyMatch_ReproducesTheInvertedBehaviour()
        {
            // Arrange
            const string requested = UpnpServices.ServiceType.ContentDirectory;

            // Act
            var matched = SsdpAdvertisement.ShouldAnswer(requested, requested, useLegacyInvertedMatch: true);
            var other = SsdpAdvertisement.ShouldAnswer(
                requested,
                UpnpServices.ServiceType.AvTransport,
                useLegacyInvertedMatch: true);

            // Assert
            matched.Should().BeFalse(
                "because the reference skipped the very type that was asked for - reproduced exactly "
                + "so the toggle is a true rollback");
            other.Should().BeTrue("because the reference answered for every other type instead");
        }

        [Test]
        public void ShouldAnswer_IsCaseInsensitive()
        {
            // Arrange
            // Act
            var answered = SsdpAdvertisement.ShouldAnswer(
                "SSDP:ALL",
                UpnpServices.RootDevice,
                useLegacyInvertedMatch: false);

            // Assert
            answered.Should().BeTrue("because header values from consumer devices vary in case");
        }

        [Test]
        public void CreateStableDeviceId_IsStableAcrossCalls_AndDiffersPerAddress()
        {
            // Arrange
            // Act
            var first = UpnpDeviceIdentity.CreateStableDeviceId("NAS", IPAddress.Parse("192.168.1.50"), 26_851);
            var again = UpnpDeviceIdentity.CreateStableDeviceId("NAS", IPAddress.Parse("192.168.1.50"), 26_851);
            var otherAddress = UpnpDeviceIdentity.CreateStableDeviceId("NAS", IPAddress.Parse("192.168.1.51"), 26_851);
            var otherMachine = UpnpDeviceIdentity.CreateStableDeviceId("OTHER", IPAddress.Parse("192.168.1.50"), 26_851);

            // Assert
            again.Should().Be(first,
                "because a restart must not look like a new device - the reference generated a fresh "
                + "GUID every start, so renderers showed the server twice until the old entry expired");
            otherAddress.Should().NotBe(first, "because each interface advertises its own identity");
            otherMachine.Should().NotBe(first, "because two machines must not claim the same identity");
        }

        [Test]
        public void DescriptionUrl_PointsAtTheAdvertisedAddressAndPort()
        {
            // Assert
            _identity.DescriptionUrl.ToString().Should().Be(
                $"http://192.168.1.50:26851/media/description.xml?uuid={_identity.DeviceId}",
                "because this URL is the LOCATION header a renderer follows to read the description");
        }
    }
}
