using OpenQA.Selenium;

namespace DlnaServer.UiTests
{
    /// <summary>
    /// The state uploading ships in. Turning it on needs a restart, so the switched-on page needs a second
    /// Host and is not driven here.
    /// </summary>
    [TestFixture]
    internal sealed class UploadPageTest
    {
        private static AdminBrowser Browser => UiTestSetup.Browser;

        [TearDown]
        public void TearDown()
        {
            Browser.AttachScreenshotOnFailure();
        }

        [Test]
        public void Open_UploadShippedOff_ShowsNoFilePicker()
        {
            // Arrange
            // Act
            Browser.Open("/admin/upload");
            var notice = Browser.TextOf(By.TagName("main"));

            // Assert
            notice.Should().Contain("Uploading is switched off",
                "because the page says why nothing can be uploaded");
            Browser.IsPresent(By.CssSelector("input[type='file']")).Should().BeFalse(
                "because with uploading off there is no endpoint a picked file could be sent to");
        }
    }
}
