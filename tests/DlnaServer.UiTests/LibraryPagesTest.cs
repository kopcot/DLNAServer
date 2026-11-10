using OpenQA.Selenium;

namespace DlnaServer.UiTests
{
    /// <summary>
    /// The three pages with route parameters, reached the way an operator reaches them: by following links,
    /// except the photo page, opened by the address its link carries.
    /// </summary>
    [TestFixture]
    internal sealed class LibraryPagesTest
    {
        private static readonly By _heading = By.TagName("h1");

        private static readonly By _holidayLink =
            By.XPath($"//main//a[starts-with(@href,'/admin/library/') and normalize-space()='{AdminHost.SubfolderName}']");

        private static readonly By _smallPreviewLink = AdminBrowser.PreviewLink(name: "small");

        private static readonly By _largePreviewLink = AdminBrowser.PreviewLink(name: "large");

        private static AdminBrowser Browser => UiTestSetup.Browser;

        [TearDown]
        public void TearDown()
        {
            Browser.CheckAfterTest();
        }

        [Test]
        public void OpenSourceFolder_FromLibraryRoot_ListsSubfolderAndFiles()
        {
            // Arrange
            // Act
            OpenSourceFolder();

            // Assert
            Browser.WaitUntil(
                condition: () => Browser.IsDisplayed(by: _holidayLink),
                because: "the source folder holds a subfolder with a picture in it");
            Browser.WaitUntil(
                condition: () => Browser.IsDisplayed(by: _smallPreviewLink) && Browser.IsDisplayed(by: _largePreviewLink),
                because: "each file in the folder links to its own preview page");
        }

        [Test]
        public void OpenFile_FromFolder_ShowsPreviewWithWayBack()
        {
            // Arrange
            OpenSourceFolder();

            // Act
            OpenSmallPreview();

            // Assert
            Browser.WaitUntil(
                condition: () => Browser.IsDisplayed(by: By.XPath("//main//a[normalize-space()='Back to folder']")),
                because: "a file opened from a folder offers the way back to it");
            Browser.WaitUntil(
                condition: () => Browser.IsDisplayed(by: By.XPath("//main//a[normalize-space()='Download']")),
                because: "every file can be downloaded from its page");
            Browser.AssertCircuitWithoutError();
        }

        [Test]
        public void OpenPhoto_FromPreview_ShowsTheImage()
        {
            // Arrange
            OpenSourceFolder();
            OpenSmallPreview();

            // Act
            // Followed by its address: on a desktop-wide window the full-screen button covers the link, which
            // is how the phone layout reaches the photo page.
            var photoLink = By.CssSelector("main a[href^='/admin/photo/']");
            var photoPage = Browser.Read(read: d => d.FindElement(photoLink).GetDomAttribute("href"));
            Browser.Open(path: photoPage!);

            // Assert
            Browser.WaitUntil(
                condition: () => ((IJavaScriptExecutor)Browser.Driver).ExecuteScript(
                    "const img = document.querySelector('main img'); return img !== null && img.complete && img.naturalWidth > 0;") is true,
                because: "the photo page must show the picture, and a rendered URL is not a working one");
        }

        private static void OpenSourceFolder()
        {
            var sourceFolder = By.XPath($"//main//a[normalize-space()='{AdminHost.SourceFolderName}']");
            Browser.Open(path: "/admin/library");
            Browser.ActUntil(
                action: () => Browser.Driver.FindElement(sourceFolder).Click(),
                done: () => Browser.IsDisplayed(by: _holidayLink),
                because: "a source folder's link opens its listing");
        }

        private static void OpenSmallPreview()
        {
            Browser.ActUntil(
                action: () => Browser.Driver.FindElement(_smallPreviewLink).Click(),
                done: () => Browser.Driver.FindElement(_heading).Text == "small",
                because: "a file's link opens its page, titled with the file's own title");
        }
    }
}
