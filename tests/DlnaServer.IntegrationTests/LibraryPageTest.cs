using DlnaServer.Admin.Pages;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers which message the Library page's <em>Recently added</em> refresh is allowed to clear.
    /// </summary>
    [TestFixture]
    internal sealed class LibraryPageTest
    {
        [Test]
        public void WithoutRecentRefreshFailure_ForAnotherActionsMessage_KeepsIt()
        {
            // Arrange
            const string message = "The folder could not be excluded. The server was busy - try again in a moment.";

            // Act
            var remaining = Library.WithoutRecentRefreshFailure(message: message);

            // Assert
            remaining.Should().Be(message,
                "because a refresh tick used to clear whatever was showing, so another action's result "
                + "disappeared within thirty seconds");
        }

        [Test]
        public void WithoutRecentRefreshFailure_ForItsOwnFailure_ClearsIt()
        {
            // Act
            var remaining = Library.WithoutRecentRefreshFailure(
                message: "Recently added could not be refreshed. The server was busy - try again in a moment.");

            // Assert
            remaining.Should().BeNull("because the next tick retries, and a success must not leave the old failure up");
        }
    }
}
