using DlnaServer.Upnp.Didl;
using DlnaServer.Upnp.Soap.ContentDirectory;

namespace DlnaServer.UnitTests.Upnp
{
    /// <summary>
    /// Pins the <c>Result</c> a Browse reply carries before its document is assigned.
    /// </summary>
    [TestFixture]
    internal sealed class BrowseResponseTest
    {
        [Test]
        public void Result_BeforeDidlIsAssigned_IsTheSerialisedEmptyDocument()
        {
            // Arrange
            var expected = DidlSerializer.Serialize(new DidlDocument());

            // Act
            var result = new BrowseResponse().Result;

            // Assert
            result.Should().Be(expected,
                "because serialising the empty document once per process must not change a byte of what "
                + "a reply without a document carries");
        }

        [Test]
        public void Result_BeforeDidlIsAssigned_IsSharedRatherThanSerialisedPerResponse()
        {
            // Arrange
            // Act
            var first = new BrowseResponse().Result;
            var second = new BrowseResponse().Result;

            // Assert
            ReferenceEquals(first, second).Should().BeTrue(
                "because every Browse assigns its own document, so serialising an empty one per response "
                + "was work thrown away on the hottest path in the service");
        }

        [Test]
        public void Result_AfterDidlIsAssigned_IsThatDocumentSerialised()
        {
            // Arrange
            var document = new DidlDocument
            {
                Containers = [new DidlContainer { ObjectId = "1", ParentId = "0", Title = "Films" }],
            };

            // Act
            var response = new BrowseResponse { Didl = document };

            // Assert
            response.Result.Should().Be(DidlSerializer.Serialize(document),
                "because assigning the document is what produces the Result the renderer reads");
        }
    }
}
