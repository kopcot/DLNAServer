using DlnaServer.Host.Uploads;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Guards the logger category the host filters on to route uploads into their own log file.
    /// </summary>
    [TestFixture]
    internal sealed class UploadSecurityLogTest
    {
        /// <summary>
        /// The category is a literal, so nothing but this test stops a namespace or type rename from
        /// silently emptying <c>logs/uploadSecurity.log</c> - the filter would match nothing, the lines
        /// would fall back into the application log, and the security record would look like it had
        /// simply stopped happening.
        /// </summary>
        [Test]
        public void Category_MatchesTheTypeThatWritesIt()
        {
            // Arrange
            // Act
            var actual = typeof(UploadSecurityLog).FullName;

            // Assert
            actual.Should().Be(UploadSecurityLog.Category,
                "because the host filters the upload sink on this exact source context, and a mismatch "
                + "disables that log file without any error");
        }
    }
}
