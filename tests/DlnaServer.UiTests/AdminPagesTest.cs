using OpenQA.Selenium;

namespace DlnaServer.UiTests
{
    [TestFixture]
    internal sealed class AdminPagesTest
    {
        private const string ErrorBoundaryText = "Something went wrong on this page";

        private static AdminBrowser Browser => UiTestSetup.Browser;

        [TearDown]
        public void TearDown()
        {
            Browser.AttachScreenshotOnFailure();
        }

        [TestCase("/admin", "Dashboard")]
        [TestCase("/admin/library", "Library")]
        [TestCase("/admin/library/search/files", "Search files")]
        [TestCase("/admin/library/search", "Search files")]
        [TestCase("/admin/library/search/folders", "Search folders")]
        [TestCase("/admin/cache", "Recently served files")]
        [TestCase("/admin/settings", "Settings")]
        [TestCase("/admin/settings/temporary", "Temporary settings")]
        [TestCase("/admin/maintenance", "Maintenance")]
        [TestCase("/admin/help", "Help")]
        [TestCase("/admin/about", "About")]
        [TestCase("/admin/upload", "Add files")]
        public void Open_EachAdminPage_RendersItsHeadingWithoutTheErrorBoundary(string path, string heading)
        {
            // Arrange
            // Act
            Browser.Open(path);
            var title = Browser.TextOf(By.TagName("h1"));

            // Assert
            title.Should().Be(heading,
                "because every admin page opens with its own title as the first heading");
            Browser.Driver.PageSource.Should().NotContain(ErrorBoundaryText,
                "because a page that threw is replaced by the layout's error notice");
        }

        [Test]
        public async Task Open_Dashboard_LoadsTheStylesheet()
        {
            // Arrange
            Browser.Open("/admin");

            // Act
            var href = Browser.Read(static d => d.FindElement(By.CssSelector("link[rel='stylesheet']")).GetDomAttribute("href"))!;
            using var response = await UiTestSetup.Host.Http.GetAsync(UiTestSetup.Host.Admin(href));

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue(
                $"because the page links {href} and an admin UI without its stylesheet is unusable");
        }

        [Test]
        public void Open_Dashboard_NavLinksEveryPageButTheShippedOffUpload()
        {
            // Arrange
            Browser.Open("/admin");
            _ = Browser.WaitFor(By.TagName("h1"));

            // Act
            var hrefs = Browser.Read(static d => d.FindElements(By.CssSelector("nav a"))
                .Select(static a => a.GetDomAttribute("href"))
                .ToList());

            // Assert
            hrefs.Should().Contain(
                [
                    "/admin", "/admin/library", "/admin/library/search/folders", "/admin/library/search/files",
                    "/admin/cache", "/admin/settings", "/admin/settings/temporary", "/admin/maintenance",
                    "/admin/help", "/admin/about",
                ],
                "because the navigation is the only way between the pages");
            hrefs.Should().NotContain("/admin/upload",
                "because the upload page is not offered while uploading is switched off");
        }
    }
}
