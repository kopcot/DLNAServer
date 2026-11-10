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
            Browser.CheckAfterTest();
        }

        [Test]
        public void Open_UploadShippedOff_ShowsNoFilePicker()
        {
            // Arrange
            // Act
            Browser.Open(path: "/admin/upload");
            var notice = Browser.TextOf(by: By.TagName("main"));

            // Assert
            notice.Should().Contain("Uploading is switched off",
                "because the page says why nothing can be uploaded");
            Browser.IsPresent(by: By.CssSelector("input[type='file']")).Should().BeFalse(
                "because with uploading off there is no endpoint a picked file could be sent to");
        }
    }
}
