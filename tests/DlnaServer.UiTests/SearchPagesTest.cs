using OpenQA.Selenium;

namespace DlnaServer.UiTests
{
    [TestFixture]
    internal sealed class SearchPagesTest
    {
        private static readonly By _nameInput = By.Id("name");
        private static readonly By _searchButton = By.XPath("//main//button[normalize-space()='Search']");
        private static readonly By _resetButton = By.XPath("//main//button[normalize-space()='Reset']");
        private static readonly By _anyPreviewLink = By.CssSelector("main a[href^='/admin/preview/']");

        private static AdminBrowser Browser => UiTestSetup.Browser;

        [TearDown]
        public void TearDown()
        {
            Browser.AttachScreenshotOnFailure();
        }

        [Test]
        public void Search_FileByName_FindsOnlyThatFileThenResetClearsIt()
        {
            // Arrange
            var small = By.XPath("//main//a[starts-with(@href,'/admin/preview/') and contains(normalize-space(),'small')]");
            var large = By.XPath("//main//a[starts-with(@href,'/admin/preview/') and contains(normalize-space(),'large')]");
            Browser.Open("/admin/library/search/files");

            // Act
            Browser.ActUntil(
                action: () => Search(name: "small"),
                done: () => Browser.IsPresent(small) && !Browser.IsPresent(large));
            Browser.WaitFor(_resetButton).Click();
            var isCleared = Browser.Read(_ => !Browser.IsPresent(_anyPreviewLink));

            // Assert
            isCleared.Should().BeTrue(
                "because Reset forgets the criteria and the results they found");
        }

        [Test]
        public void Search_FolderByName_FindsOnlyThatFolder()
        {
            // Arrange
            var holiday = By.XPath($"//main//a[starts-with(@href,'/admin/library/') and contains(.,'{AdminHost.SubfolderName}')]");
            var sourceFolder = By.XPath(
                $"//main//a[starts-with(@href,'/admin/library/') and normalize-space(text())='{AdminHost.SourceFolderName}']");
            Browser.Open("/admin/library/search/folders");

            // Act
            Browser.ActUntil(
                action: () => Search(name: AdminHost.SubfolderName),
                done: () => Browser.IsPresent(holiday) && !Browser.IsPresent(sourceFolder));

            // Assert
            Browser.IsPresent(holiday).Should().BeTrue(
                "because a folder search by name links the matching folder's listing");
        }

        /// <remarks>
        /// Typed again on every attempt: text typed into the prerendered page is discarded when the circuit's
        /// first interactive render replaces it.
        /// </remarks>
        private static void Search(string name)
        {
            var input = Browser.Driver.FindElement(_nameInput);
            input.Clear();
            input.SendKeys(name);
            Browser.Driver.FindElement(_searchButton).Click();
        }
    }
}
