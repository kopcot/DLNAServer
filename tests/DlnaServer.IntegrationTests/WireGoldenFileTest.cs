using System.Net;
using System.Text.RegularExpressions;
using DlnaServer.Upnp.Didl;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Compares this server's wire output against documents captured from the reference server running.
    /// </summary>
    /// <remarks>
    /// <c>[WIRE-1]</c> claims the wire format is "byte-identical, golden-file tested". Nothing implemented
    /// it, and a literal byte comparison cannot be implemented: object identifiers are <c>PublicId</c>
    /// GUIDs and the reference mints its own, so the same file is a different id in each server on every
    /// run. The device UDN, <c>dc:date</c> and the host and port in every URL have the same problem.
    /// <para>
    /// So the volatile fields are masked and the rest is compared. What survives normalisation is
    /// everything that actually breaks a television - element order, namespace declarations, attribute
    /// spelling, escaping and the DLNA <c>protocolInfo</c> strings.
    /// </para>
    /// <para>
    /// See <c>GoldenFiles/README.md</c> for how the captures were produced and what they already proved.
    /// </para>
    /// </remarks>
    [TestFixture]
    internal sealed class WireGoldenFileTest
    {
        private static readonly string _goldenDirectory =
            Path.Combine(TestContext.CurrentContext.TestDirectory, "GoldenFiles");

        [Test]
        public void GoldenFiles_AreDeployedBesideTheTests()
        {
            // Arrange
            var expected = new[]
            {
                "reference-description.xml",
                "reference-browse-response.xml",
                "reference-sort-capabilities.xml",
                "reference-search-capabilities.xml",
            };

            // Act
            var present = expected.Where(name => File.Exists(Path.Combine(_goldenDirectory, name))).ToArray();

            // Assert
            present.Should().BeEquivalentTo(expected,
                "because a golden test that silently finds no golden file passes while proving nothing");
        }

        /// <summary>
        /// The order this server writes DIDL elements in must match the order the reference writes them.
        /// </summary>
        /// <remarks>
        /// Read out of the capture rather than out of this codebase's own <c>Order=</c> attributes, so it
        /// is the reference that decides what correct means. This is the assertion that would have caught
        /// a reordering during the rewrite, which no test could have caught before.
        /// </remarks>
        [Test]
        public void SerializedItem_FollowsTheReferenceElementOrder()
        {
            // Arrange
            var referenceOrder = ReadElementOrder(ExtractReferenceDidl());

            // Act
            var ours = DidlSerializer.Serialize(CreateItemOnlyDocument());
            var ourOrder = ReadElementOrder(ours);

            // Assert
            var shared = referenceOrder.Where(name => ourOrder.Contains(name)).ToArray();
            var oursFiltered = ourOrder.Where(name => referenceOrder.Contains(name)).ToArray();

            oursFiltered.Should().Equal(shared,
                "because every element both servers emit must appear in the same relative order - a "
                + "renderer that reads them positionally shows an empty listing otherwise");
        }

        /// <summary>
        /// The reference declares these namespaces on DIDL-Lite, and so must this server.
        /// </summary>
        [Test]
        public void SerializedDocument_DeclaresTheSameNamespacesAsTheReference()
        {
            // Arrange
            var reference = ExtractReferenceDidl();
            var expected = ReadNamespaceUris(reference);

            // Act
            var ourNamespaces = ReadNamespaceUris(DidlSerializer.Serialize(CreateItemOnlyDocument()));

            // Assert
            ourNamespaces.Should().Contain(expected.Where(static uri => !uri.Contains("XMLSchema", StringComparison.Ordinal)),
                "because a renderer resolving upnp: or dc: against a missing declaration drops the element; "
                + "the XMLSchema pair is SoapCore boilerplate the reference adds and carries no metadata");
        }

        /// <summary>
        /// Records that the reference answers GetSortCapabilities with the wrong element name.
        /// </summary>
        /// <remarks>
        /// Its <c>GetSortCapabilitiesResponse</c> carries <c>&lt;SearchCaps&gt;</c> rather than
        /// <c>&lt;SortCaps&gt;</c> - a copy-paste defect in the reference, confirmed against the live
        /// server rather than inferred from its source. This server emits the spec-correct
        /// <c>&lt;SortCaps&gt;</c>, so it is a deliberate divergence. The test exists so that fact stays
        /// visible: if someone later "fixes" this server to match the reference byte for byte, they should
        /// have to delete this test to do it.
        /// </remarks>
        [Test]
        public void ReferenceSortCapabilities_UsesTheWrongElementName_WhichThisServerDoesNotCopy()
        {
            // Arrange
            var reference = File.ReadAllText(Path.Combine(_goldenDirectory, "reference-sort-capabilities.xml"));

            // Act
            var hasSearchCaps = reference.Contains("<SearchCaps>", StringComparison.Ordinal);
            var hasSortCaps = reference.Contains("<SortCaps>", StringComparison.Ordinal);

            // Assert
            hasSearchCaps.Should().BeTrue(
                "because the captured reference response really does use SearchCaps inside "
                + "GetSortCapabilitiesResponse");
            hasSortCaps.Should().BeFalse(
                "because the reference never emits the correct element name here, which is what makes "
                + "this server's SortCaps a divergence rather than a match");
        }

        /// <summary>
        /// Pulls the DIDL-Lite document out of the captured SOAP response, undoing the XML escaping.
        /// </summary>
        private static string ExtractReferenceDidl()
        {
            var soap = File.ReadAllText(Path.Combine(_goldenDirectory, "reference-browse-response.xml"));
            var match = Regex.Match(soap, "<Result>(?<didl>.*?)</Result>", RegexOptions.Singleline);

            match.Success.Should().BeTrue("because the capture must contain a Result element to compare against");

            return WebUtility.HtmlDecode(match.Groups["didl"].Value);
        }

        private static List<string> ReadElementOrder(string didl)
        {
            var itemStart = didl.IndexOf("<item ", StringComparison.Ordinal);

            itemStart.Should().BeGreaterThanOrEqualTo(0, "because the comparison is about an item's elements");

            var names = new List<string>();

            foreach (var match in Regex.Matches(didl[itemStart..], @"<(?<name>[A-Za-z:][\w:.-]*)").Cast<Match>())
            {
                var name = match.Groups["name"].Value;

                if (!string.Equals(name, "item", StringComparison.Ordinal))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        private static List<string> ReadNamespaceUris(string xml)
        {
            return Regex.Matches(xml, @"xmlns(?::[\w.-]+)?=""(?<uri>[^""]+)""")
                .Cast<Match>()
                .Select(static match => match.Groups["uri"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <remarks>
        /// Shaped like the capture: one image item with a media resource and a thumbnail resource, so the
        /// element sets line up and the order comparison is meaningful.
        /// </remarks>
        private static DidlDocument CreateItemOnlyDocument()
        {
            return new DidlDocument
            {
                Items =
                [
                    new DidlItem
                    {
                        ObjectId = "11111111-1111-1111-1111-111111111111",
                        ParentId = "0",
                        Class = "object.item.imageItem",
                        Title = "Sample.png",
                        Date = "2026-01-01T00:00:00.0000000",
                        Resources =
                        [
                            new DidlResource
                            {
                                ProtocolInfo = "http-get:*:image/png:DLNA.ORG_PN=PNG",
                                Size = "70",
                                Url = "http://127.0.0.1:26852/fileserver/file/11111111-1111-1111-1111-111111111111",
                            },
                        ],
                        ThumbnailResource = new DidlResource
                        {
                            ProtocolInfo = "http-get:*:image/jpeg:DLNA.ORG_PN=JPEG_TN",
                            Url = "http://127.0.0.1:26852/fileserver/file/11111111-1111-1111-1111-111111111111",
                        },
                        AlbumArtUri = "http://127.0.0.1:26852/fileserver/file/11111111-1111-1111-1111-111111111111",
                        Icon = "http://127.0.0.1:26852/fileserver/file/11111111-1111-1111-1111-111111111111",
                    },
                ],
            };
        }
    }
}
