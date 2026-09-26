using System.Net;
using DlnaServer.Core.Gena;
using DlnaServer.Host.Gena;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the subscription store, whose whole purpose is that the handshake is honest: an identifier
    /// the server hands out is one it later recognises, and one it forgets when told to.
    /// </summary>
    [TestFixture]
    internal sealed class SubscriptionStoreTest
    {
        private static readonly string[] _callback = ["http://192.168.1.5:9000/notify"];
        private static readonly TimeSpan _granted = TimeSpan.FromMinutes(30);
        private static readonly IPAddress _television = IPAddress.Parse("192.168.1.5");

        private MutableTimeProvider _time = null!;
        private SubscriptionStore _store = null!;

        [SetUp]
        public void SetUp()
        {
            _time = new MutableTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
            _store = new SubscriptionStore(_time);
        }

        [Test]
        public void Add_IssuesAUuidPrefixedIdentifier()
        {
            // Arrange
            // Act
            var subscription = AddFromTelevision(_granted);

            // Assert
            subscription.Should().NotBeNull("because an empty store accepts a subscriber");
            subscription!.Sid.Should().StartWith("uuid:",
                "because GENA requires the subscription identifier to be a uuid: URI");
            subscription.ExpiresUtc.Should().Be(_time.GetUtcNow().Add(_granted),
                "because the granted lifetime is measured from when it was granted");
            _store.Count.Should().Be(1, "because one subscription is now held");
        }

        [Test]
        public void Renew_ExtendsTheExpiryFromNow()
        {
            // Arrange
            var subscription = AddFromTelevision(_granted)!;
            _time.Advance(TimeSpan.FromMinutes(20));

            // Act
            var renewed = _store.Renew(subscription.Sid, _granted);

            // Assert
            renewed.Should().NotBeNull("because the subscription had ten minutes left when renewed");
            renewed!.ExpiresUtc.Should().Be(_time.GetUtcNow().Add(_granted),
                "because a renewal restarts the lifetime rather than adding to the old expiry");
            renewed.Sid.Should().Be(subscription.Sid,
                "because a renewal keeps the identifier the renderer already knows");
            renewed.ServiceId.Should().Be("ContentDirectory",
                "because a renewal carries no service and must not lose the one it subscribed to");
        }

        [Test]
        public void Renew_ForAnUnknownIdentifier_Fails()
        {
            // Arrange
            // Act
            var renewed = _store.Renew("uuid:00000000-0000-0000-0000-000000000000", _granted);

            // Assert
            renewed.Should().BeNull(
                "because the endpoint must answer 412 rather than silently create a subscription");
        }

        /// <summary>
        /// A lapsed subscription is not revived: the renderer believes it is dead, and the specification
        /// has it subscribe afresh.
        /// </summary>
        [Test]
        public void Renew_AfterTheSubscriptionLapsed_Fails()
        {
            // Arrange
            var subscription = AddFromTelevision(_granted)!;
            _time.Advance(_granted.Add(TimeSpan.FromSeconds(1)));

            // Act
            var renewed = _store.Renew(subscription.Sid, _granted);

            // Assert
            renewed.Should().BeNull("because the granted lifetime had elapsed");
            _store.Count.Should().Be(0, "because a lapsed subscription is dropped rather than kept");
        }

        /// <summary>
        /// The defect this store exists to fix: the reference's UNSUBSCRIBE reports success and removes
        /// nothing, so a renderer that unsubscribed stays subscribed for its whole granted lifetime.
        /// </summary>
        [Test]
        public void Remove_ForgetsTheSubscription()
        {
            // Arrange
            var subscription = AddFromTelevision(_granted)!;

            // Act
            var isRemoved = _store.Remove(subscription.Sid);

            // Assert
            isRemoved.Should().BeTrue("because the identifier was held");
            _store.Count.Should().Be(0, "because unsubscribing must actually release the subscription");
            _store.Renew(subscription.Sid, _granted).Should().BeNull(
                "because a removed identifier is no longer renewable");
        }

        [Test]
        public void Remove_ForAnUnknownIdentifier_Fails()
        {
            // Arrange
            // Act
            var isRemoved = _store.Remove("uuid:00000000-0000-0000-0000-000000000000");

            // Assert
            isRemoved.Should().BeFalse(
                "because the endpoint answers 412 for an identifier it never issued");
        }

        [Test]
        public void Count_ExcludesLapsedSubscriptions()
        {
            // Arrange
            _ = AddFromTelevision(TimeSpan.FromMinutes(1));
            _ = _store.Add(
                serviceId: "ConnectionManager",
                callbackUrls: _callback,
                granted: TimeSpan.FromMinutes(30),
                subscriber: _television);

            // Act
            _time.Advance(TimeSpan.FromMinutes(5));

            // Assert
            _store.Count.Should().Be(1,
                "because a subscription nobody renewed is gone, and only the longer one survives");
        }

        /// <summary>
        /// A renderer that subscribes without ever unsubscribing must not be able to grow the store
        /// without limit - the same bounding rule the media backlog follows.
        /// </summary>
        [Test]
        public void Add_BeyondTheBound_IsRefusedRatherThanGrowing()
        {
            // Arrange
            for (var index = 0; index < 64; index++)
            {
                _store.Add(
                        serviceId: "ContentDirectory",
                        callbackUrls: _callback,
                        granted: _granted,
                        subscriber: DeviceNumber(index))
                    .Should().NotBeNull($"because subscription {index} is within the bound");
            }

            // Act
            var overflow = _store.Add(
                serviceId: "ContentDirectory",
                callbackUrls: _callback,
                granted: _granted,
                subscriber: DeviceNumber(64));

            // Assert
            overflow.Should().BeNull(
                "because the endpoint must answer 503 rather than hold subscriptions without limit");
        }

        /// <summary>
        /// Reaching the bound must not be permanent: lapsed entries free their slots.
        /// </summary>
        [Test]
        public void Add_AfterTheBoundIsReachedAndEntriesLapse_Succeeds()
        {
            // Arrange
            for (var index = 0; index < 64; index++)
            {
                _ = _store.Add(
                    serviceId: "ContentDirectory",
                    callbackUrls: _callback,
                    granted: _granted,
                    subscriber: DeviceNumber(index));
            }

            // Act
            _time.Advance(_granted.Add(TimeSpan.FromSeconds(1)));
            var afterExpiry = _store.Add(
                serviceId: "ContentDirectory",
                callbackUrls: _callback,
                granted: _granted,
                subscriber: DeviceNumber(64));

            // Assert
            afterExpiry.Should().NotBeNull(
                "because pruning lapsed subscriptions is what keeps the bound from becoming permanent");
        }

        /// <summary>
        /// One device subscribing in a loop must not take every slot and refuse each television after it.
        /// </summary>
        [Test]
        public void Add_BeyondOneDevicesShare_IsRefusedWhileOtherDevicesAreStillAccepted()
        {
            // Arrange
            for (var index = 0; index < 8; index++)
            {
                AddFromTelevision(_granted)
                    .Should().NotBeNull($"because subscription {index} is within one device's share");
            }

            // Act
            var overflow = AddFromTelevision(_granted);
            var otherDevice = _store.Add(
                serviceId: "ContentDirectory",
                callbackUrls: _callback,
                granted: _granted,
                subscriber: IPAddress.Parse("192.168.1.6"));

            // Assert
            overflow.Should().BeNull(
                "because a ninth subscription from one device takes the 503 path rather than another slot");
            otherDevice.Should().NotBeNull(
                "because the store still has room, and another device's share is untouched");
        }

        /// <summary>
        /// A dual-stack socket reports an IPv4 peer as <c>::ffff:a.b.c.d</c>; that is the same device.
        /// </summary>
        [Test]
        public void Add_FromAnIPv4MappedAddress_CountsAgainstTheSameDevice()
        {
            // Arrange
            for (var index = 0; index < 8; index++)
            {
                _ = AddFromTelevision(_granted);
            }

            // Act
            var mapped = _store.Add(
                serviceId: "ContentDirectory",
                callbackUrls: _callback,
                granted: _granted,
                subscriber: _television.MapToIPv6());

            // Assert
            mapped.Should().BeNull(
                "because the mapped form is the same television, not a ninth device with a fresh share");
        }

        /// <summary>
        /// An IPv6 device chooses its own address inside its /64, so a fresh address per SUBSCRIBE must not
        /// buy it a fresh share.
        /// </summary>
        [Test]
        public void Add_FromNineAddressesInOneIPv6Prefix_CountsThemAsOneDevice()
        {
            // Arrange
            for (var index = 1; index <= 8; index++)
            {
                _store.Add(
                        serviceId: "ContentDirectory",
                        callbackUrls: _callback,
                        granted: _granted,
                        subscriber: IPAddress.Parse($"2001:db8:1:2::{index:x}"))
                    .Should().NotBeNull($"because subscription {index} is within the prefix's share");
            }

            // Act
            var ninthInPrefix = _store.Add(
                serviceId: "ContentDirectory",
                callbackUrls: _callback,
                granted: _granted,
                subscriber: IPAddress.Parse("2001:db8:1:2:abcd:ef01:2345:6789"));
            var otherPrefix = _store.Add(
                serviceId: "ContentDirectory",
                callbackUrls: _callback,
                granted: _granted,
                subscriber: IPAddress.Parse("2001:db8:1:3::1"));

            // Assert
            ninthInPrefix.Should().BeNull(
                "because every address in one /64 is the same device, and it already holds its eight");
            otherPrefix.Should().NotBeNull(
                "because a device on another /64 has a share of its own");
        }

        [Test]
        public void Add_AfterADevicesSubscriptionsLapse_AcceptsThatDeviceAgain()
        {
            // Arrange
            for (var index = 0; index < 8; index++)
            {
                _ = AddFromTelevision(_granted);
            }

            // Act
            _time.Advance(_granted.Add(TimeSpan.FromSeconds(1)));
            var afterExpiry = AddFromTelevision(_granted);

            // Assert
            afterExpiry.Should().NotBeNull(
                "because lapsed subscriptions free a device's share just as they free the store's");
        }

        private EventSubscription? AddFromTelevision(TimeSpan granted)
        {
            return _store.Add(
                serviceId: "ContentDirectory",
                callbackUrls: _callback,
                granted: granted,
                subscriber: _television);
        }

        private static IPAddress DeviceNumber(int index)
        {
            return new IPAddress([192, 168, 2, (byte)(index + 1)]);
        }
    }
}
