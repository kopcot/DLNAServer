using DlnaServer.Upnp.Constants;
using DlnaServer.Upnp.Ssdp;

namespace DlnaServer.UnitTests.Upnp
{
    /// <summary>
    /// Pins the SSDP datagram bytes. Consumer SSDP parsers are frequently sloppy and some care about
    /// header order, so these assert the full message rather than individual fields.
    /// </summary>
    [TestFixture]
    internal sealed class SsdpMessageBuilderTest
    {
        private static readonly Guid _deviceId = new("11111111-2222-3333-4444-555555555555");
        private static readonly Uri _descriptionUrl =
            new("http://192.168.1.50:26851/media/description.xml?uuid=11111111-2222-3333-4444-555555555555");

        private const string Signature = "Linux/64bit/5.10 UPnP/1.0 DLNADOC/1.5 zen_dlna/1.0/26851";

        [Test]
        public void CreateNotify_ProducesTheExactAliveDatagram()
        {
            // Arrange
            // Act
            var message = SsdpMessageBuilder.CreateNotify(
                hostEndpoint: "239.255.255.250:1900",
                descriptionUrl: _descriptionUrl,
                serverSignature: Signature,
                notificationSubtype: "ssdp:alive",
                notificationType: UpnpServices.ServiceType.ContentDirectory,
                uniqueServiceName: SsdpMessageBuilder.CreateUniqueServiceName(
                    _deviceId,
                    UpnpServices.ServiceType.ContentDirectory));

            // Assert
            var expected =
                "NOTIFY * HTTP/1.1\r\n"
                + "HOST: 239.255.255.250:1900\r\n"
                + "CACHE-CONTROL: max-age=600\r\n"
                + $"LOCATION: {_descriptionUrl}\r\n"
                + $"SERVER: {Signature}\r\n"
                + "NTS: ssdp:alive\r\n"
                + "NT: urn:schemas-upnp-org:service:ContentDirectory:1\r\n"
                + "USN: uuid:11111111-2222-3333-4444-555555555555::urn:schemas-upnp-org:service:ContentDirectory:1\r\n"
                + "\r\n";

            message.Should().Be(expected,
                "because header names, order and CRLF line endings are all part of what devices parse");
        }

        [Test]
        public void CreateNotify_ForByebye_OnlyTheSubtypeChanges()
        {
            // Arrange
            // Act
            var alive = SsdpMessageBuilder.CreateNotify(
                "239.255.255.250:1900", _descriptionUrl, Signature, "ssdp:alive",
                UpnpServices.RootDevice,
                SsdpMessageBuilder.CreateUniqueServiceName(_deviceId, UpnpServices.RootDevice));

            var byebye = SsdpMessageBuilder.CreateNotify(
                "239.255.255.250:1900", _descriptionUrl, Signature, "ssdp:byebye",
                UpnpServices.RootDevice,
                SsdpMessageBuilder.CreateUniqueServiceName(_deviceId, UpnpServices.RootDevice));

            // Assert
            byebye.Should().Be(alive.Replace("ssdp:alive", "ssdp:byebye", StringComparison.Ordinal),
                "because a byebye is the same announcement with a different NTS");
        }

        /// <summary>
        /// The reference sends every announcement to the limited broadcast address as well as the
        /// multicast group, and HOST carries whichever it is addressed to.
        /// </summary>
        [Test]
        public void CreateNotify_CarriesTheDestinationInTheHostHeader()
        {
            // Arrange
            // Act
            var broadcast = SsdpMessageBuilder.CreateNotify(
                "255.255.255.255:1900", _descriptionUrl, Signature, "ssdp:alive",
                UpnpServices.RootDevice,
                SsdpMessageBuilder.CreateUniqueServiceName(_deviceId, UpnpServices.RootDevice));

            // Assert
            broadcast.Should().Contain("HOST: 255.255.255.255:1900\r\n",
                "because HOST mirrors the endpoint the datagram is actually sent to");
        }

        [Test]
        public void CreateSearchResponse_ProducesTheExactReplyDatagram()
        {
            // Arrange
            var nowUtc = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

            // Act
            var message = SsdpMessageBuilder.CreateSearchResponse(
                nowUtc,
                _descriptionUrl,
                Signature,
                UpnpServices.MediaServer,
                SsdpMessageBuilder.CreateUniqueServiceName(_deviceId, UpnpServices.MediaServer));

            // Assert
            var expected =
                "HTTP/1.1 200 OK\r\n"
                + "CACHE-CONTROL: max-age=600\r\n"
                + "DATE: Tue, 01 Sep 2026 12:00:00 GMT\r\n"
                + "EXT: \r\n"
                + $"LOCATION: {_descriptionUrl}\r\n"
                + $"SERVER: {Signature}\r\n"
                + "ST: urn:schemas-upnp-org:device:MediaServer:1\r\n"
                + "USN: uuid:11111111-2222-3333-4444-555555555555::urn:schemas-upnp-org:device:MediaServer:1\r\n"
                + "\r\n";

            message.Should().Be(expected,
                "because the M-SEARCH reply is what a renderer uses to locate the description document");
        }

        [Test]
        public void CreateSearchResponse_EmitsAnEmptyExtHeader()
        {
            // Arrange
            // Act
            var message = SsdpMessageBuilder.CreateSearchResponse(
                DateTime.UtcNow, _descriptionUrl, Signature, UpnpServices.RootDevice, "uuid:x");

            // Assert
            message.Should().Contain("EXT: \r\n",
                "because UPnP requires the EXT header to be present with no value");
        }

        [Test]
        public void CreateUniqueServiceName_ForABareUuidTarget_ReturnsItUnchanged()
        {
            // Arrange
            // Act
            var usn = SsdpMessageBuilder.CreateUniqueServiceName(_deviceId, $"uuid:{_deviceId}");

            // Assert
            usn.Should().Be($"uuid:{_deviceId}",
                "because the device's own uuid target is its own USN, with no :: suffix");
        }

        [Test]
        public void CreateUniqueServiceName_ForATypedTarget_CombinesUuidAndType()
        {
            // Arrange
            // Act
            var usn = SsdpMessageBuilder.CreateUniqueServiceName(_deviceId, UpnpServices.RootDevice);

            // Assert
            usn.Should().Be($"uuid:{_deviceId}::upnp:rootdevice",
                "because a typed announcement identifies both the device and the advertised type");
        }
    }
}
