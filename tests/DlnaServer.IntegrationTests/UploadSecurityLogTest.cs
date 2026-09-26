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

        [TestCase("film\r\nupload from=10.0.0.1 outcome=Uploaded.mp4", @"film\u000D\u000Aupload from=10.0.0.1 outcome=Uploaded.mp4")]
        [TestCase("a\"b", @"a\u0022b")]
        [TestCase("a\u2028b", @"a\u2028b")]
        public void Escape_RewritesWhatCouldEndTheLineOrTheQuotedValue(string value, string expected)
        {
            // Arrange
            // Act
            var escaped = UploadSecurityLog.Escape(value);

            // Assert
            escaped.Should().Be(expected,
                "because an untrusted name written raw could end the log line and forge the next one, or "
                + "close its quoted value early");
        }

        [Test]
        public void Escape_WithNothingToEscape_ReturnsTheSameInstance()
        {
            // Arrange
            const string value = "/media/Films/film (2024).mp4";

            // Act
            var escaped = UploadSecurityLog.Escape(value);

            // Assert
            escaped.Should().BeSameAs(value,
                "because the common case is a clean value, and it should cost no allocation");
        }
    }
}
