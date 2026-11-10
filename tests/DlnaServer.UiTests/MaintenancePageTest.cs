using OpenQA.Selenium;

namespace DlnaServer.UiTests
{
    /// <summary>
    /// Arms and cancels only. Every action on this page restarts, stops or empties the one Host the whole run
    /// shares, so nothing here is ever confirmed.
    /// </summary>
    [TestFixture]
    internal sealed class MaintenancePageTest
    {
        private const string ServerPanel = "//h2[normalize-space()='Server']/..";

        private static AdminBrowser Browser => UiTestSetup.Browser;

        [TearDown]
        public void TearDown()
        {
            Browser.AttachScreenshotOnFailure();
        }

        [Test]
        public async Task ArmRestart_ThenCancel_LeavesTheServerRunning()
        {
            // Arrange
            var restart = By.XPath($"{ServerPanel}//button[normalize-space()='Restart server']");
            var confirm = By.XPath($"{ServerPanel}//button[normalize-space()='Confirm restart']");
            var cancel = By.XPath($"{ServerPanel}//button[normalize-space()='Cancel']");
            Browser.Open("/admin/maintenance");
            Browser.ActUntil(action: () => Browser.Driver.FindElement(restart).Click(), done: () => Browser.IsPresent(confirm));

            // Act
            Browser.ActUntil(action: () => Browser.Driver.FindElement(cancel).Click(), done: () => Browser.IsPresent(restart));
            using var health = await UiTestSetup.Host.Http.GetAsync(UiTestSetup.Host.Admin("/admin/health"));

            // Assert
            Browser.IsPresent(confirm).Should().BeFalse(
                "because cancelling disarms the action rather than leaving the confirm button armed");
            health.IsSuccessStatusCode.Should().BeTrue(
                "because cancelling a restart must not restart the server");
        }
    }
}
