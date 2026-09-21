using DlnaServer.Host.Gena;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Pins the <c>TIMEOUT</c> header grammar. The reference sends <c>TimeSpan.ToString()</c>, so its
    /// header reads <c>00:30:00</c> - a value the grammar does not allow.
    /// </summary>
    [TestFixture]
    internal sealed class GenaTimeoutTest
    {
        [Test]
        public void Format_ProducesTheSecondPrefixedForm()
        {
            // Arrange
            // Act
            var formatted = GenaTimeout.Format(TimeSpan.FromMinutes(30));

            // Assert
            formatted.Should().Be("Second-1800",
                "because this is the only form the GENA TIMEOUT header defines");
        }

        [Test]
        public void Grant_ForARequestWithinTheAllowedRange_HonoursIt()
        {
            // Arrange
            // Act
            var granted = GenaTimeout.Grant("Second-600");

            // Assert
            granted.Should().Be(TimeSpan.FromMinutes(10),
                "because a server may grant what was asked for when it is reasonable");
        }

        [TestCase("Second-infinite", TestName = "Grant_ForAnInfiniteRequest_GrantsTheMaximum")]
        [TestCase("Second-99999", TestName = "Grant_ForARequestOverTheMaximum_GrantsTheMaximum")]
        [TestCase("", TestName = "Grant_ForAnAbsentHeader_GrantsTheMaximum")]
        [TestCase("nonsense", TestName = "Grant_ForAMalformedHeader_GrantsTheMaximum")]
        [TestCase("Second-", TestName = "Grant_ForAPrefixWithNoNumber_GrantsTheMaximum")]
        [TestCase("Second--60", TestName = "Grant_ForANegativeRequest_GrantsTheMaximum")]
        public void Grant_ForAnythingNotUsable_GrantsTheMaximum(string requested)
        {
            // Arrange
            // Act
            var granted = GenaTimeout.Grant(requested);

            // Assert
            granted.Should().Be(GenaTimeout.Maximum,
                "because a server may always grant less than asked, and refusing the subscription over a "
                + "header it can substitute for would be worse");
        }

        /// <summary>
        /// A renderer asking for seconds would otherwise renew in a tight loop.
        /// </summary>
        [Test]
        public void Grant_ForARequestUnderTheMinimum_GrantsTheMinimum()
        {
            // Arrange
            // Act
            var granted = GenaTimeout.Grant("Second-1");

            // Assert
            granted.Should().Be(GenaTimeout.Minimum,
                "because a one-second subscription would have the renderer renewing continuously");
        }

        [Test]
        public void Grant_IsCaseInsensitiveOnThePrefix()
        {
            // Arrange
            // Act
            var granted = GenaTimeout.Grant("second-600");

            // Assert
            granted.Should().Be(TimeSpan.FromMinutes(10),
                "because HTTP header values from real devices vary in case");
        }

        [Test]
        public void FormatOfAGrantedValue_RoundTripsThroughGrant()
        {
            // Arrange
            // Act
            var granted = GenaTimeout.Grant("Second-600");
            var reparsed = GenaTimeout.Grant(GenaTimeout.Format(granted));

            // Assert
            reparsed.Should().Be(granted,
                "because what the server writes into the header must be something it can read back");
        }
    }
}
