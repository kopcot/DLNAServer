using DlnaServer.Upnp.Didl;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the serialised DIDL-Lite bytes, which nothing exercised before.
    /// </summary>
    /// <remarks>
    /// The Browse body is the one document every listing reads, and it had no test at all: the mapper
    /// was covered on its object model, so element order, namespace prefixes and XML escaping - the three
    /// things that decide whether a television renders a listing - were only ever verified by pointing a
    /// television at the server.
    /// <para>
    /// The expected element order here is not invented. It is the order the reference actually emits,
    /// read off the capture in <c>GoldenFiles/reference-browse-response.xml</c>: <c>res</c>,
    /// <c>upnp:class</c>, <c>dc:title</c>, <c>dc:date</c>, the thumbnail <c>res</c>,
    /// <c>upnp:albumArtURI</c>, then <c>upnp:icon</c>.
    /// </para>
    /// </remarks>
    [TestFixture]
    internal sealed class DidlSerializerTest
    {
        [Test]
        public void Serialize_CarriesNoDeclarationAndNoIndentation()
        {
            // Arrange
            var document = CreateDocument();

            // Act
            var xml = DidlSerializer.Serialize(document);

            // Assert
            xml.Should().StartWith("<DIDL-Lite",
                "because the result is embedded as escaped text inside the SOAP Result element, where an "
                + "XML declaration would make the outer document invalid");
            xml.Should().NotContain("\n",
                "because the compact form is what goes on the wire");
        }

        /// <summary>
        /// XmlSerializer writes an attribute without its prefix when the attribute's namespace is the
        /// element's own - which turned <c>sec:type</c> into a bare <c>type</c> the first time this ran.
        /// </summary>
        [Test]
        public void Serialize_ForASubtitle_WritesSamsungsCaptionElementWithItsPrefixedType()
        {
            // Arrange
            var document = CreateDocument();
            document.Items[0].CaptionInfo = new DidlCaptionInfo { Type = "srt", Url = "http://host/fileserver/subtitle/a.srt" };

            // Act
            var xml = DidlSerializer.Serialize(document);

            // Assert
            xml.Should().Contain("<sec:CaptionInfoEx sec:type=\"srt\">http://host/fileserver/subtitle/a.srt</sec:CaptionInfoEx>",
                "because Samsung televisions read the subtitle's format from sec:type");
        }

        [Test]
        public void Serialize_DeclaresEveryNamespaceARendererExpects()
        {
            // Arrange
            var document = CreateDocument();

            // Act
            var xml = DidlSerializer.Serialize(document);

            // Assert
            xml.Should().Contain("xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\"",
                "because DIDL-Lite must be the default namespace or every element is unqualified");
            xml.Should().Contain("xmlns:dc=\"http://purl.org/dc/elements/1.1/\"",
                "because dc:title and dc:date are read from that namespace");
            xml.Should().Contain("xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\"",
                "because upnp:class is what tells a renderer the object type");
            xml.Should().Contain("xmlns:dlna=\"urn:schemas-dlna-org:metadata-1-0/\"",
                "because the reference declares it and some renderers key off its presence");
            xml.Should().Contain("xmlns:sec=",
                "because Samsung televisions look for the sec namespace");
        }

        /// <summary>
        /// Element order is contractual, not cosmetic.
        /// </summary>
        [Test]
        public void Serialize_WritesElementsInTheOrderTheReferenceEmits()
        {
            // Arrange
            var document = CreateDocument();

            // Act
            var xml = DidlSerializer.Serialize(document);

            // Assert
            // Scoped to the item: the container is written first and carries its own upnp:class, so
            // searching the whole document would compare elements from two different objects.
            var itemStart = xml.IndexOf("<item ", StringComparison.Ordinal);
            var item = xml[itemStart..];

            var res = item.IndexOf("<res ", StringComparison.Ordinal);
            var upnpClass = item.IndexOf("<upnp:class>", StringComparison.Ordinal);
            var title = item.IndexOf("<dc:title>", StringComparison.Ordinal);
            var date = item.IndexOf("<dc:date>", StringComparison.Ordinal);
            var albumArt = item.IndexOf("<upnp:albumArtURI>", StringComparison.Ordinal);

            res.Should().BeLessThan(upnpClass, "because res is written first, as in the reference");
            upnpClass.Should().BeLessThan(title, "because upnp:class precedes dc:title in the reference");
            title.Should().BeLessThan(date, "because dc:date follows dc:title in the reference");
            date.Should().BeLessThan(albumArt, "because the thumbnail metadata trails the descriptive elements");
        }

        /// <summary>
        /// An ampersand in a file name must not break the enclosing SOAP document.
        /// </summary>
        /// <remarks>
        /// This is the escaping case that has no other cover. Media libraries are full of names like
        /// <c>Tom &amp; Jerry</c>, and an unescaped ampersand makes the whole Browse response unparseable,
        /// so a renderer shows an empty folder rather than one bad title.
        /// </remarks>
        [Test]
        public void Serialize_EscapesMarkupInATitle()
        {
            // Arrange
            var document = CreateDocument();
            document.Items[0].Title = "Tom & Jerry <best> \"picks\"";

            // Act
            var xml = DidlSerializer.Serialize(document);

            // Assert
            xml.Should().Contain("Tom &amp; Jerry &lt;best&gt;",
                "because an unescaped ampersand or angle bracket makes the entire response unparseable");
            xml.Should().NotContain("Tom & Jerry",
                "because the raw ampersand must not survive into the document");
        }

        [Test]
        public void Serialize_WritesContainersBeforeItems()
        {
            // Arrange
            var document = CreateDocument();

            // Act
            var xml = DidlSerializer.Serialize(document);

            // Assert
            xml.IndexOf("<container ", StringComparison.Ordinal)
                .Should()
                .BeLessThan(
                    xml.IndexOf("<item ", StringComparison.Ordinal),
                    "because a renderer draws folders above files and takes that order from the document");
        }

        private static DidlDocument CreateDocument()
        {
            return new DidlDocument
            {
                Containers =
                [
                    new DidlContainer
                    {
                        ObjectId = "22222222-2222-2222-2222-222222222222",
                        ParentId = "0",
                        Class = "object.container.storageFolder",
                        Title = "Movies",
                        ChildCount = "3",
                    },
                ],
                Items =
                [
                    new DidlItem
                    {
                        ObjectId = "11111111-1111-1111-1111-111111111111",
                        ParentId = "0",
                        Class = "object.item.videoItem",
                        Title = "Film",
                        Date = "2026-01-01T00:00:00.0000000",
                        Resources =
                        [
                            new DidlResource
                            {
                                ProtocolInfo = "http-get:*:video/x-matroska:DLNA.ORG_PN=MATROSKA",
                                Size = "1024",
                                Duration = "1:30:15.250",
                                Resolution = "1920x1080",
                                Bitrate = "1000000",
                                Url = "http://192.168.1.50:26852/fileserver/file/11111111-1111-1111-1111-111111111111",
                            },
                        ],
                        AlbumArtUri = "http://192.168.1.50:26852/fileserver/thumbnail/33333333-3333-3333-3333-333333333333",
                    },
                ],
            };
        }
    }
}
