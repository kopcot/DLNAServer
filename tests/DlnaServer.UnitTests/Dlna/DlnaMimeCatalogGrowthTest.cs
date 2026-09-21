using DlnaServer.Core.Dlna;

namespace DlnaServer.UnitTests.Dlna
{
    /// <summary>
    /// Guards the two things that can go wrong when MIME types are added to the catalog: an existing
    /// extension changing owner, and an existing member changing number.
    /// </summary>
    /// <remarks>
    /// Both were real. Adding the reference's remaining 102 types made <c>.mp3</c> resolve to
    /// <c>VideoXMpeg</c> instead of <c>AudioMpeg3</c>, because <c>CreateExtensionIndex</c> is
    /// first-writer-wins ordered by enum value and the bands run Video &lt; Audio &lt; Image - so a *new*
    /// video type outranks an *existing* audio one. A new entry therefore never claims an extension that
    /// already resolves somewhere.
    /// <para>
    /// The numbers matter because <c>MediaFileEntity.Mime</c> is stored with <c>HasConversion&lt;int&gt;()</c>:
    /// renumbering a member silently retypes every indexed file that carried it.
    /// </para>
    /// </remarks>
    [TestFixture]
    internal sealed class DlnaMimeCatalogGrowthTest
    {
        /// <summary>
        /// Extensions more than one MIME lays claim to, pinned to the type that owned them before the
        /// catalog grew. These are exactly the ones a newly added type could take over.
        /// </summary>
        private static readonly (string Extension, DlnaMime Owner)[] _contestedExtensions =
        [
            (".aif", DlnaMime.AudioXAiff),
            (".aifc", DlnaMime.AudioXAiff),
            (".aiff", DlnaMime.AudioXAiff),
            (".asx", DlnaMime.VideoXMsAsf),
            (".avi", DlnaMime.VideoXMsvideo),
            (".bmp", DlnaMime.ImageBmp),
            (".jfif", DlnaMime.ImageJpeg),
            (".jpe", DlnaMime.ImageJpeg),
            (".jpeg", DlnaMime.ImageJpeg),
            (".jpg", DlnaMime.ImageJpeg),
            (".mp3", DlnaMime.AudioMpeg3),
            (".srt", DlnaMime.SubtitleSubrip),
            (".tif", DlnaMime.ImageTiff),
            (".tiff", DlnaMime.ImageTiff),
            (".ttml", DlnaMime.SubtitleTtmlXml),
            (".wma", DlnaMime.AudioXMswma),
        ];

        /// <summary>
        /// The numbers stored in the database for the types that existed before the catalog grew.
        /// </summary>
        private static readonly (DlnaMime Mime, int Value)[] _persistedValues =
        [
            (DlnaMime.Undefined, 0),
            (DlnaMime.Video3gpp, 1),
            (DlnaMime.VideoAvi, 2),
            (DlnaMime.VideoMp2T, 3),
            (DlnaMime.VideoMp4, 4),
            (DlnaMime.VideoMpeg, 5),
            (DlnaMime.VideoOgg, 6),
            (DlnaMime.VideoQuicktime, 7),
            (DlnaMime.VideoWebm, 8),
            (DlnaMime.VideoXFlv, 9),
            (DlnaMime.VideoXMatroska, 10),
            (DlnaMime.VideoXMsAsf, 11),
            (DlnaMime.VideoXMsvideo, 12),
            (DlnaMime.VideoXMswmv, 13),
            (DlnaMime.AudioAac, 100),
            (DlnaMime.AudioFlac, 101),
            (DlnaMime.AudioMp4, 102),
            (DlnaMime.AudioMpeg, 103),
            (DlnaMime.AudioMpeg3, 104),
            (DlnaMime.AudioOgg, 105),
            (DlnaMime.AudioWav, 106),
            (DlnaMime.AudioXAiff, 107),
            (DlnaMime.AudioXMswma, 108),
            (DlnaMime.AudioXWav, 109),
            (DlnaMime.ImageBmp, 200),
            (DlnaMime.ImageGif, 201),
            (DlnaMime.ImageJpeg, 202),
            (DlnaMime.ImagePng, 203),
            (DlnaMime.ImageSvgXml, 204),
            (DlnaMime.ImageTiff, 205),
            (DlnaMime.ImageWebp, 206),
            (DlnaMime.ImageXIcon, 207),
            (DlnaMime.SubtitleMicroDVD, 300),
            (DlnaMime.SubtitleSubrip, 301),
            (DlnaMime.SubtitleTtmlXml, 302),
            (DlnaMime.SubtitleVtt, 303),
            (DlnaMime.SubtitleXSubrip, 304),
        ];

        [Test]
        public void TryGetByFileExtension_ForAContestedExtension_StillResolvesToItsOriginalOwner()
        {
            foreach (var (extension, expected) in _contestedExtensions)
            {
                // Act
                var found = DlnaMimeCatalog.TryGetByFileExtension(extension, out var actual);

                // Assert
                found.Should().BeTrue($"because {extension} was resolvable before the catalog grew");

                actual.Should().Be(expected,
                    $"because {extension} already resolved to {expected}, and a type added later must "
                        + "not take an extension over - files of that type would silently change kind");
            }
        }

        /// <summary>
        /// A member's number is in the database for every file indexed under it.
        /// </summary>
        [Test]
        public void DlnaMime_KeepsTheNumbersAlreadyWrittenToTheDatabase()
        {
            foreach (var (mime, expected) in _persistedValues)
            {
                // Assert
                ((int)mime).Should().Be(expected,
                    $"because {mime} is stored as {expected} for every file already indexed, and "
                        + "renumbering it would retype them all without touching a single row");
            }
        }

        /// <summary>
        /// Every catalog entry must be reachable, or a file of that type resolves to nothing.
        /// </summary>
        [Test]
        public void EveryCatalogEntry_HasAMimeStringAndAKnownMedia()
        {
            foreach (var info in DlnaMimeCatalog.All)
            {
                // Assert
                info.MimeString.Should().NotBeNullOrWhiteSpace(
                    $"because {info.Mime} is offered for selection and would otherwise be announced as nothing");

                info.Media.Should().NotBe(DlnaMedia.Unknown,
                    $"because {info.Mime} needs a media kind to pick a processor and a UPnP class");
            }
        }
    }
}
