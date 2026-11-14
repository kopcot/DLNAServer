using OpenQA.Selenium;

namespace DlnaServer.UiTests
{
    [TestFixture]
    internal sealed class AdminPagesTest
    {
        private const string ErrorBoundaryText = "Something went wrong on this page";

        // Whether a page attaches a Blazor circuit after prerendering, which is what can die unseen.
        private const bool Interactive = true;
        private const bool Static = false;

        private static readonly By _heading = By.TagName("h1");

        private static AdminBrowser Browser => UiTestSetup.Browser;

        [TearDown]
        public void TearDown()
        {
            Browser.CheckAfterTest();
        }

        [TestCase("/admin", "Dashboard", Interactive)]
        [TestCase("/admin/library", "Library", Interactive)]
        [TestCase("/admin/library/search/files", "Search files", Interactive)]
        [TestCase("/admin/library/search", "Search files", Interactive)]
        [TestCase("/admin/library/search/folders", "Search folders", Interactive)]
        [TestCase("/admin/cache", "Recently served files", Interactive)]
        [TestCase("/admin/settings", "Settings", Interactive)]
        [TestCase("/admin/settings/temporary", "Temporary settings", Interactive)]
        [TestCase("/admin/maintenance", "Maintenance", Interactive)]
        [TestCase("/admin/help", "Help", Static)]
        [TestCase("/admin/about", "About", Static)]
        [TestCase("/admin/upload", "Add files", Static)]
        public void Open_EachAdminPage_RendersItsHeadingWithoutTheErrorBoundary(
            string path,
            string heading,
            bool isInteractive)
        {
            // Arrange
            // Act
            Browser.Open(path: path);
            var title = Browser.TextOf(by: _heading);

            // Assert
            title.Should().Be(heading,
                "because every admin page opens with its own title as the first heading");
            Browser.Driver.PageSource.Should().NotContain(ErrorBoundaryText,
                "because a page that threw while prerendering is replaced by the layout's error notice");
            if (isInteractive)
            {
                Browser.AssertCircuitWithoutError();
            }
        }

        [Test]
        public async Task Open_Dashboard_LoadsTheStylesheet()
        {
            // Arrange
            Browser.Open(path: "/admin");

            // Act
            var href = Browser.Read(read: static d => d.FindElement(By.CssSelector("link[rel='stylesheet']")).GetDomAttribute("href"))!;
            using var response = await UiTestSetup.Host.Http.GetAsync(UiTestSetup.Host.Admin(path: href));

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue(
                $"because the page links {href} and an admin UI without its stylesheet is unusable");
        }

        [Test]
        public void FollowNavLink_FromDashboard_OpensItsPageAndListsEveryPageButTheShippedOffUpload()
        {
            // Arrange
            string[] menu =
            [
                "/admin", "/admin/library", "/admin/library/search/folders", "/admin/library/search/files",
                "/admin/cache", "/admin/settings", "/admin/settings/temporary", "/admin/maintenance",
                "/admin/help", "/admin/about",
            ];
            var searchFolders = By.CssSelector("nav a[href='/admin/library/search/folders']");
            Browser.Open(path: "/admin");

            // Act
            Browser.ActUntil(
                action: () => Browser.Driver.FindElement(searchFolders).Click(),
                done: () => Browser.Driver.FindElement(_heading).Text == "Search folders",
                because: "following a navigation link opens the page it names");

            // Assert
            foreach (var href in menu)
            {
                Browser.WaitUntil(
                    condition: () => Browser.IsDisplayed(by: By.CssSelector($"nav a[href='{href}']")),
                    because: $"the navigation is the only way between the pages, and {href} must be shown in it");
            }

            Browser.IsPresent(by: By.CssSelector("nav a[href='/admin/upload']")).Should().BeFalse(
                "because the upload page is not offered while uploading is switched off");
        }
    }
}
