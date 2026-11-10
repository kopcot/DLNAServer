using OpenQA.Selenium;

namespace DlnaServer.UiTests
{
    /// <summary>
    /// Only the restart is driven, and only armed and cancelled: confirming it - or the page's stop and
    /// rebuild actions - would end or empty the one Host the whole run shares.
    /// </summary>
    [TestFixture]
    internal sealed class MaintenancePageTest
    {
        private const string ServerPanel = "//h2[normalize-space()='Server']/..";

        private static AdminBrowser Browser => UiTestSetup.Browser;

        [TearDown]
        public void TearDown()
        {
            Browser.CheckAfterTest();
        }

        [Test]
        public void ArmRestart_ThenCancel_LeavesTheServerRunning()
        {
            // Arrange
            var restart = By.XPath($"{ServerPanel}//button[normalize-space()='Restart server']");
            var confirm = By.XPath($"{ServerPanel}//button[normalize-space()='Confirm restart']");
            var cancel = By.XPath($"{ServerPanel}//button[normalize-space()='Cancel']");
            Browser.Open(path: "/admin/maintenance");
            Browser.ActUntil(
                action: () => Browser.Driver.FindElement(restart).Click(),
                done: () => Browser.IsDisplayed(by: confirm),
                because: "the first click on Restart server arms it and offers the confirmation");

            // Act
            // Assert
            Browser.ActUntil(
                action: () => Browser.Driver.FindElement(cancel).Click(),
                done: () => Browser.IsDisplayed(by: restart) && !Browser.IsPresent(by: confirm),
                because: "cancelling disarms the restart rather than leaving the confirm button armed");
        }
    }
}
