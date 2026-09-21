using DlnaServer.Host.Gena;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers <c>CALLBACK</c> header parsing. The reference stores the header verbatim, angle brackets
    /// and all, which is not an address anything could be posted to.
    /// </summary>
    [TestFixture]
    internal sealed class GenaCallbackTest
    {
        [Test]
        public void Parse_UnwrapsASingleUrl()
        {
            // Arrange
            // Act
            var urls = GenaCallback.Parse("<http://192.168.1.5:9000/notify>");

            // Assert
            urls.Should().Equal(["http://192.168.1.5:9000/notify"],
                "because the angle brackets are the header's delimiters, not part of the address");
        }

        /// <summary>
        /// Multiple callbacks are concatenated with no separator between the brackets.
        /// </summary>
        [Test]
        public void Parse_ReadsEveryUrlInOrder()
        {
            // Arrange
            // Act
            var urls = GenaCallback.Parse("<http://192.168.1.5:9000/a><http://192.168.1.5:9000/b>");

            // Assert
            urls.Should().Equal(
                ["http://192.168.1.5:9000/a", "http://192.168.1.5:9000/b"],
                "because a renderer may offer several delivery addresses and the order is its preference");
        }

        [Test]
        public void Parse_TolerantOfWhitespaceBetweenAndInsideBrackets()
        {
            // Arrange
            // Act
            var urls = GenaCallback.Parse(" < http://192.168.1.5:9000/a > < http://192.168.1.5:9000/b > ");

            // Assert
            urls.Should().HaveCount(2, "because real devices pad the header in ways the grammar tolerates");
            urls[0].Should().Be("http://192.168.1.5:9000/a", "because the address itself is trimmed");
        }

        [TestCase("", TestName = "Parse_ForAnEmptyHeader_ReturnsNothing")]
        [TestCase("http://192.168.1.5:9000/notify", TestName = "Parse_WithoutBrackets_ReturnsNothing")]
        [TestCase("<>", TestName = "Parse_ForEmptyBrackets_ReturnsNothing")]
        [TestCase("<http://192.168.1.5:9000/notify", TestName = "Parse_ForAnUnclosedBracket_ReturnsNothing")]
        [TestCase("</notify>", TestName = "Parse_ForARelativeUrl_ReturnsNothing")]
        [TestCase("<ftp://192.168.1.5/notify>", TestName = "Parse_ForANonHttpScheme_ReturnsNothing")]
        public void Parse_ForAnUnusableHeader_ReturnsNothing(string header)
        {
            // Arrange
            // Act
            var urls = GenaCallback.Parse(header);

            // Assert
            urls.Should().BeEmpty(
                "because an address that cannot be delivered to must refuse the subscription rather than "
                + "confirm one that can never work");
        }

        /// <summary>
        /// One unusable entry alongside a usable one leaves the subscription viable.
        /// </summary>
        [Test]
        public void Parse_SkipsUnusableEntriesAndKeepsTheRest()
        {
            // Arrange
            // Act
            var urls = GenaCallback.Parse("</relative><http://192.168.1.5:9000/good>");

            // Assert
            urls.Should().Equal(["http://192.168.1.5:9000/good"],
                "because a renderer offering one good address and one bad one can still be notified");
        }
    }
}
