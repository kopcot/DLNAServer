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
            Browser.CheckAfterTest();
        }

        [Test]
        public void Search_FileByName_FindsOnlyThatFileThenResetClearsIt()
        {
            // Arrange
            var small = AdminBrowser.PreviewLink(name: "small");
            var large = AdminBrowser.PreviewLink(name: "large");
            Browser.Open(path: "/admin/library/search/files");
            Browser.ActUntil(
                action: () => Search(name: "small"),
                done: () => Browser.IsDisplayed(by: small) && !Browser.IsPresent(by: large),
                because: "a file search by name lists the files with that name and leaves the others out");

            // Act
            // Assert
            Browser.ActUntil(
                action: () => Browser.Driver.FindElement(_resetButton).Click(),
                done: () => !Browser.IsPresent(by: _anyPreviewLink),
                because: "Reset forgets the criteria and the results they found");
        }

        [Test]
        public void Search_FolderByName_FindsOnlyThatFolder()
        {
            // Arrange
            var holiday = By.XPath($"//main//a[starts-with(@href,'/admin/library/') and contains(.,'{AdminHost.SubfolderName}')]");
            var sourceFolder = By.XPath(
                $"//main//a[starts-with(@href,'/admin/library/') and normalize-space(text())='{AdminHost.SourceFolderName}']");
            Browser.Open(path: "/admin/library/search/folders");

            // Act
            // Assert
            Browser.ActUntil(
                action: () => Search(name: AdminHost.SubfolderName),
                done: () => Browser.IsDisplayed(by: holiday) && !Browser.IsPresent(by: sourceFolder),
                because: "a folder search by name links the matching folder's listing and leaves the others out");
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
