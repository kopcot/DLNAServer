using DlnaServer.Core.Dlna;

namespace DlnaServer.UnitTests.Dlna
{
    [TestFixture]
    internal sealed class DlnaItemClassExtensionsTest
    {
        /// <summary>
        /// These strings go straight into DIDL-Lite. They are verbatim from the reference.
        /// </summary>
        [TestCase(DlnaItemClass.Container, "object.container")]
        [TestCase(DlnaItemClass.ContainerStorageFolder, "object.container.storageFolder")]
        [TestCase(DlnaItemClass.ContainerMusicAlbum, "object.container.musicAlbum")]
        [TestCase(DlnaItemClass.Generic, "object.item")]
        [TestCase(DlnaItemClass.AudioItem, "object.item.audioItem")]
        [TestCase(DlnaItemClass.AudioItemMusicTrack, "object.item.audioItem.musicTrack")]
        [TestCase(DlnaItemClass.VideoItem, "object.item.videoItem")]
        [TestCase(DlnaItemClass.VideoItemMovie, "object.item.videoItem.movie")]
        [TestCase(DlnaItemClass.VideoItemMusicVideoClip, "object.item.videoItem.musicVideoClip")]
        [TestCase(DlnaItemClass.ImageItem, "object.item.imageItem")]
        [TestCase(DlnaItemClass.ImageItemPhoto, "object.item.imageItem.photo")]
        [TestCase(DlnaItemClass.TextItem, "object.item.textItem")]
        public void ToUpnpClass_ForClass_MatchesReferenceImplementation(DlnaItemClass itemClass, string expected)
        {
            // Arrange
            // Act
            var actual = itemClass.ToUpnpClass();

            // Assert
            actual.Should().Be(expected,
                "because upnp:class is matched literally by renderers and must stay byte-identical");
        }

        [Test]
        public void ToUpnpClass_ForUnknown_FallsBackToGenericItem()
        {
            // Arrange
            // Act
            var actual = DlnaItemClass.Unknown.ToUpnpClass();

            // Assert
            actual.Should().Be("object.item",
                "because a stray default enum value must not fail the whole Browse response - "
                + "the reference threw NotImplementedException here");
        }

        [Test]
        public void ToUpnpClass_ForEveryDeclaredValue_ReturnsAnObjectClass()
        {
            // Arrange
            var all = Enum.GetValues<DlnaItemClass>();

            // Act
            var invalid = all
                .Where(static c => !c.ToUpnpClass().StartsWith("object.", StringComparison.Ordinal))
                .ToArray();

            // Assert
            invalid.Should().BeEmpty("because every upnp:class value is rooted at 'object.'");
        }
    }
}
