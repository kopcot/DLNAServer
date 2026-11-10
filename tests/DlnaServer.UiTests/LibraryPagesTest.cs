using OpenQA.Selenium;

namespace DlnaServer.UiTests
{
    /// <summary>
    /// The three pages with route parameters, reached the way an operator reaches them: by following links.
    /// </summary>
    [TestFixture]
    internal sealed class LibraryPagesTest
    {
        private static readonly By _holidayLink =
            By.XPath($"//main//a[starts-with(@href,'/admin/library/') and normalize-space()='{AdminHost.SubfolderName}']");

        private static readonly By _smallPreviewLink =
            By.XPath("//main//a[starts-with(@href,'/admin/preview/') and contains(normalize-space(),'small')]");

        private static readonly By _largePreviewLink =
            By.XPath("//main//a[starts-with(@href,'/admin/preview/') and contains(normalize-space(),'large')]");

        private static AdminBrowser Browser => UiTestSetup.Browser;

        [TearDown]
        public void TearDown()
        {
            Browser.AttachScreenshotOnFailure();
        }

        [Test]
        public void OpenSourceFolder_FromLibraryRoot_ListsSubfolderAndFiles()
        {
            // Arrange
            // Act
            OpenSourceFolder();

            // Assert
            Browser.IsPresent(_holidayLink).Should().BeTrue(
                "because the source folder holds a subfolder with a picture in it");
            Browser.IsPresent(_smallPreviewLink).Should().BeTrue(
                "because each file in the folder links to its own preview page");
            Browser.IsPresent(_largePreviewLink).Should().BeTrue(
                "because each file in the folder links to its own preview page");
        }

        [Test]
        public void OpenFile_FromFolder_ShowsPreviewWithWayBack()
        {
            // Arrange
            OpenSourceFolder();

            // Act
            Browser.WaitFor(_smallPreviewLink).Click();
            var title = Browser.Read(static d => d.FindElement(By.TagName("h1")).Text is var t && t == "small" ? t : null);

            // Assert
            title.Should().Be("small",
                "because a file's page is titled with the file's own title");
            Browser.IsPresent(By.XPath("//main//a[normalize-space()='Back to folder']")).Should().BeTrue(
                "because a file opened from a folder offers the way back to it");
            Browser.IsPresent(By.XPath("//main//a[normalize-space()='Download']")).Should().BeTrue(
                "because every file can be downloaded from its page");
        }

        [Test]
        public void OpenPhoto_FromPreview_ShowsTheImage()
        {
            // Arrange
            OpenSourceFolder();
            Browser.WaitFor(_smallPreviewLink).Click();

            // Act
            // Followed by its address: on a desktop the full-screen button sits over the link, which is the
            // phone layout's way to the photo page.
            var photoPage = Browser.Read(static d => d.FindElement(By.CssSelector("main a[href^='/admin/photo/']")).GetDomAttribute("href"));
            Browser.Open(photoPage!);
            var isLoaded = Browser.Read(static d => ((IJavaScriptExecutor)d).ExecuteScript(
                "const img = document.querySelector('main img'); return img !== null && img.complete && img.naturalWidth > 0;") is true);

            // Assert
            isLoaded.Should().BeTrue(
                "because the photo page must show the picture, and a rendered URL is not a working one");
        }

        private static void OpenSourceFolder()
        {
            Browser.Open("/admin/library");
            Browser.WaitFor(By.XPath($"//main//a[normalize-space()='{AdminHost.SourceFolderName}']")).Click();
            _ = Browser.WaitFor(_holidayLink);
        }
    }
}
