using System.Xml;
using DlnaServer.Upnp.Description;

namespace DlnaServer.UnitTests.Upnp
{
    /// <summary>
    /// Pins the bytes of <c>description.xml</c>. This is the first document a renderer fetches, and a
    /// change here can make a TV stop recognising the server entirely.
    /// </summary>
    [TestFixture]
    internal sealed class DeviceDescriptionBuilderTest
    {
        private static readonly Guid _deviceId = new("11111111-2222-3333-4444-555555555555");

        private static DeviceDescription CreateDescription()
        {
            return new DeviceDescription(
                _deviceId,
                FriendlyName: "ZEN DLNA Server (TESTBOX)",
                ManufacturerName: "Kopco",
                ManufacturerUrl: "https://github.com/kopcot/DLNAServer",
                ModelName: "1.0.0.0");
        }

        /// <summary>
        /// Verified against the real round-trip rather than assumed: XmlDocument preserves a declaration
        /// that was present in the loaded string, so the reference emits one and so must this.
        /// </summary>
        [Test]
        public void Build_KeepsTheXmlDeclaration()
        {
            // Arrange
            // Act
            var xml = DeviceDescriptionBuilder.Build(CreateDescription());

            // Assert
            xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?><root",
                "because XmlDocument carries the declaration through OuterXml, and the declaration "
                + "immediately precedes the root with no whitespace after re-serialisation");
        }

        /// <summary>
        /// The round-trip collapses the template's indentation, so the wire form is compact.
        /// </summary>
        [Test]
        public void Build_EmitsCompactXmlWithoutTemplateIndentation()
        {
            // Arrange
            // Act
            var xml = DeviceDescriptionBuilder.Build(CreateDescription());

            // Assert
            var newlineThenTab = string.Concat(Environment.NewLine, "	");
            xml.Should().NotContain(newlineThenTab,
                "because re-serialising through XmlDocument drops the source formatting - the bytes on "
                + "the wire are the compact form devices were tested against");
            xml.Should().Contain("<specVersion><major>1</major><minor>0</minor></specVersion>",
                "because elements sit adjacent with no separating whitespace");
        }

        [Test]
        public void Build_DeclaresTheSamsungNamespaceWithTheDlnaSuffix()
        {
            // Arrange
            // Act
            var xml = DeviceDescriptionBuilder.Build(CreateDescription());

            // Assert
            xml.Should().Contain("xmlns:sec=\"http://www.sec.co.kr/dlna\"",
                "because description.xml uses the /dlna-suffixed Samsung namespace, unlike DIDL-Lite "
                + "which uses http://www.sec.co.kr/ - the two documents genuinely differ");
        }

        [Test]
        public void Build_DeclaresBothDlnaDocProfiles()
        {
            // Arrange
            // Act
            var xml = DeviceDescriptionBuilder.Build(CreateDescription());

            // Assert
            xml.Should().Contain("<dlna:X_DLNADOC>DMS-1.50</dlna:X_DLNADOC>",
                "because the standard DMS profile must be advertised");
            xml.Should().Contain("<dlna:X_DLNADOC>M-DMS-1.50</dlna:X_DLNADOC>",
                "because the mobile profile is advertised alongside it to satisfy both strict and "
                + "lenient certification checks");
        }

        [Test]
        public void Build_AdvertisesTheDeviceIdAsUdn()
        {
            // Arrange
            // Act
            var xml = DeviceDescriptionBuilder.Build(CreateDescription());

            // Assert
            xml.Should().Contain($"<UDN>uuid:{_deviceId}</UDN>",
                "because the UDN is how a renderer correlates the description with the SSDP announcement");
        }

        [Test]
        public void Build_AdvertisesAllFourServicesWithMatchingControlAndScpdUrls()
        {
            // Arrange
            // Act
            var xml = DeviceDescriptionBuilder.Build(CreateDescription());

            // Assert
            foreach (var (serviceType, controlUrl, scpdUrl) in new[]
            {
                ("ContentDirectory:1", "/ContentDirectoryService.asmx", "/SCPD/contentDirectory.xml"),
                ("ConnectionManager:1", "/ConnectionManagerService.asmx", "/SCPD/connectionManager.xml"),
                ("AVTransport:1", "/AVTransportService.asmx", "/SCPD/avTransport.xml"),
                ("X_MS_MediaReceiverRegistrar:1", "/MediaReceiverRegistrarService.asmx", "/SCPD/MediaReceiverRegistrar.xml"),
            })
            {
                xml.Should().Contain(serviceType, "because {0} must be advertised", serviceType);
                xml.Should().Contain($"<controlURL>{controlUrl}</controlURL>",
                    "because the control URL is where SOAP actions are sent");
                xml.Should().Contain($"<SCPDURL>{scpdUrl}</SCPDURL>",
                    "because the renderer fetches the SCPD to learn the service's actions");
            }
        }

        [Test]
        public void Build_AdvertisesSixIconsMatchingTheServedFiles()
        {
            // Arrange
            // Act
            var xml = DeviceDescriptionBuilder.Build(CreateDescription());

            // Assert
            foreach (var icon in new[]
            {
                "/icon/extraLarge.jpg", "/icon/extraLarge.png",
                "/icon/large.jpg", "/icon/large.png",
                "/icon/small.jpg", "/icon/small.png",
            })
            {
                xml.Should().Contain($"<url>{icon}</url>",
                    "because {0} is advertised and must be servable", icon);
            }
        }

        [Test]
        public void Build_ProducesWellFormedXml()
        {
            // Arrange
            var xml = DeviceDescriptionBuilder.Build(CreateDescription());

            // Act
            var act = () =>
            {
                var document = new XmlDocument();
                document.LoadXml(xml);
            };

            // Assert
            act.Should().NotThrow("because a renderer that cannot parse the description ignores the server");
        }

        /// <summary>
        /// A friendly name is user-supplied and reaches the document directly.
        /// </summary>
        [Test]
        public void Build_EscapesMarkupInTheFriendlyName()
        {
            // Arrange
            var description = CreateDescription() with { FriendlyName = "Tom & Jerry <living room>" };

            // Act
            var xml = DeviceDescriptionBuilder.Build(description);

            // Assert
            var act = () =>
            {
                var document = new XmlDocument();
                document.LoadXml(xml);
            };

            act.Should().NotThrow("because an unescaped ampersand would make the whole document unparseable");
            xml.Should().Contain("Tom &amp; Jerry", "because the name must survive escaping intact");
        }

        [Test]
        public void Build_IsDeterministic()
        {
            // Arrange
            // Act
            var first = DeviceDescriptionBuilder.Build(CreateDescription());
            var second = DeviceDescriptionBuilder.Build(CreateDescription());

            // Assert
            second.Should().Be(first,
                "because the same identity must always produce identical bytes - a renderer caches this "
                + "document and compares it");
        }
    }
}
