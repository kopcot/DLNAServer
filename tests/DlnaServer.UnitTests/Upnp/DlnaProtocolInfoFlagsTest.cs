using DlnaServer.Upnp.Constants;

namespace DlnaServer.UnitTests.Upnp
{
    /// <summary>
    /// The <c>protocolInfo</c> fields are now computed from named flags rather than written out as
    /// literals, so these pin the strings that reach a television.
    /// </summary>
    /// <remarks>
    /// Every expected value below is what the reference sends. Readability was the point of the change
    /// and a changed byte was never the point, so if adding or removing a flag alters one of these, that
    /// is a wire change and it should fail here rather than on a television.
    /// </remarks>
    [TestFixture]
    internal sealed class DlnaProtocolInfoFlagsTest
    {
        [Test]
        public void FlagsStreaming_IsWhatTheReferenceSends()
        {
            // Assert
            DlnaProtocolInfo.FlagsStreaming.Should().Be("21F00000000000000000000000000000",
                "because that is the value the reference advertises for audio and video, and renderers "
                    + "were validated against it");
        }

        [Test]
        public void FlagsInteractive_IsTheStreamingValueWithoutTheStreamingBit()
        {
            // Assert
            DlnaProtocolInfo.FlagsInteractive.Should().Be("20F00000000000000000000000000000",
                "because an image is fetched on demand rather than streamed");
        }

        [Test]
        public void OperationTimeSeekOnly_IsTwoBinaryDigits()
        {
            // Assert
            DlnaProtocolInfo.OperationTimeSeekOnly.Should().Be("01",
                "because DLNA.ORG_OP is byte-seek then time-seek, and only time-seek is declared");
        }

        [Test]
        public void ContentIndex_IsASingleDecimalDigit()
        {
            // Assert
            DlnaProtocolInfo.ContentIndexNone.Should().Be("0",
                "because 0 marks the resource as the media itself");

            DlnaProtocolInfo.ContentIndexThumbnail.Should().Be("1",
                "because 1 marks it as a thumbnail, which is how a television tells the two apart");
        }

        /// <summary>
        /// The flags field is 32 characters wide however few bits are set.
        /// </summary>
        [Test]
        public void Format_ForNoFlagsAtAll_IsStillThirtyTwoCharacters()
        {
            // Act
            var formatted = DlnaProtocolInfo.Format(DlnaOrgFlags.None);

            // Assert
            formatted.Should().HaveLength(32,
                "because the width is fixed by the specification, not by how much is set");

            formatted.Should().Be("00000000000000000000000000000000",
                "because no capability claimed is all zeros, not an empty field");
        }

        /// <summary>
        /// The bit each name stands for has to be the bit the specification names.
        /// </summary>
        [Test]
        public void Format_ForASingleFlag_SetsTheBitTheSpecificationNames()
        {
            // Arrange
            (DlnaOrgFlags Flag, string Expected)[] cases =
            [
                (DlnaOrgFlags.SenderPaced, "80000000"),
                (DlnaOrgFlags.TimeSeekOperation, "40000000"),
                (DlnaOrgFlags.ByteSeekOperation, "20000000"),
                (DlnaOrgFlags.PlayContainer, "10000000"),
                (DlnaOrgFlags.StreamingTransferMode, "01000000"),
                (DlnaOrgFlags.InteractiveTransferMode, "00800000"),
                (DlnaOrgFlags.BackgroundTransferMode, "00400000"),
                (DlnaOrgFlags.ConnectionStall, "00200000"),
                (DlnaOrgFlags.DlnaV15, "00100000"),
            ];

            foreach (var (flag, expected) in cases)
            {
                // Act
                var formatted = DlnaProtocolInfo.Format(flag);

                // Assert
                formatted.Should().StartWith(expected,
                    $"because {flag} is the bit the DLNA guidelines place at {expected}");
            }
        }

        [Test]
        public void Format_ForByteSeek_IsTheOtherOperationDigit()
        {
            // Assert
            DlnaProtocolInfo.Format(DlnaOrgOperation.ByteSeekSupported).Should().Be("10",
                "because byte-seek is the leading digit of DLNA.ORG_OP, which is what makes 01 mean "
                    + "time-seek rather than byte-seek");

            DlnaProtocolInfo.Format(DlnaOrgOperation.None).Should().Be("00",
                "because no seeking declared is still two digits");
        }
    }
}
