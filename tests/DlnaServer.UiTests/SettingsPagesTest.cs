using OpenQA.Selenium;

namespace DlnaServer.UiTests
{
    [TestFixture]
    internal sealed class SettingsPagesTest
    {
        private const string HiddenFoldersPanel = "//h2[normalize-space()='Show temporarily hidden folders']/..";

        private static readonly By _showHidden = By.XPath($"{HiddenFoldersPanel}//button[normalize-space()='Show now']");
        private static readonly By _hideHidden = By.XPath($"{HiddenFoldersPanel}//button[normalize-space()='Hide now']");

        private const string TelevisionTrafficPanel = "//h2[normalize-space()='Television traffic']/..";

        private static readonly By _block = By.XPath($"{TelevisionTrafficPanel}//button[normalize-space()='Block']");
        private static readonly By _unblock = By.XPath($"{TelevisionTrafficPanel}//button[normalize-space()='Unblock now']");

        private static AdminBrowser Browser => UiTestSetup.Browser;

        /// <remarks>
        /// The hidden-folder switch and the television block are process-wide, so both are put back whatever
        /// the test did - a failure half way through would otherwise leave every later listing showing the
        /// hidden folders, or the shared Host refusing televisions for an hour.
        /// </remarks>
        [TearDown]
        public void TearDown()
        {
            try
            {
                Browser.CheckAfterTest();
            }
            finally
            {
                // Each restore in its own finally, so a failure putting one back cannot leave the other set.
                try
                {
                    Restore(button: _hideHidden, because: "the hidden folders are hidden again for the tests that follow");
                }
                finally
                {
                    Restore(button: _unblock, because: "televisions are answered again for the tests that follow");
                }
            }
        }

        /// <remarks>
        /// The model name rather than the friendly name, which the test Host sets through the environment -
        /// and the environment wins over the file. Writes only the test Host's temporary config.json.
        /// </remarks>
        [Test]
        public void Save_ChangedModelName_PersistsAndShowsOnAbout()
        {
            // Arrange
            const string modelName = "UiTest model";
            var modelInput = By.Id("modelName");
            Browser.Open(path: "/admin/settings");

            // Act
            // The input is read back as well: a save racing the circuit's first render saves the old value.
            Browser.ActUntil(
                action: () =>
                {
                    var input = Browser.Driver.FindElement(modelInput);
                    input.Clear();
                    input.SendKeys(modelName);
                    Browser.Driver.FindElement(By.XPath("//main//button[normalize-space()='Save']")).Click();
                },
                done: () => Browser.Driver.PageSource.Contains("Saved.", StringComparison.Ordinal)
                    && Browser.Driver.FindElement(modelInput).GetDomProperty("value") == modelName,
                because: "Save reports that it saved the value typed");
            Browser.Open(path: "/admin/settings");
            var reloaded = Browser.Read(read: d => d.FindElement(modelInput).GetDomProperty("value"));
            Browser.Open(path: "/admin/about");
            var shown = Browser.TextOf(by: By.XPath("//dt[normalize-space()='Model']/following-sibling::dd[1]"));

            // Assert
            reloaded.Should().Be(modelName,
                "because a saved setting is written to the settings file and read back from it");
            shown.Should().Be(modelName,
                "because About shows what the televisions are told, which is the saved value");
        }

        [Test]
        public void ShowHiddenFolders_ThenHide_TogglesBack()
        {
            // Arrange
            Browser.Open(path: "/admin/settings/temporary");
            Browser.ActUntil(
                action: () => Browser.Driver.FindElement(_showHidden).Click(),
                done: () => Browser.IsDisplayed(by: _hideHidden) && !Browser.IsPresent(by: _showHidden),
                because: "Show now swaps itself for Hide now while the hidden folders are shown");

            // Act
            // Assert
            Browser.ActUntil(
                action: () => Browser.Driver.FindElement(_hideHidden).Click(),
                done: () => Browser.IsDisplayed(by: _showHidden) && !Browser.IsPresent(by: _hideHidden),
                because: "hiding the folders again puts the panel back in its starting state");
        }

        [Test]
        public void BlockTelevisions_ThenUnblock_TogglesBack()
        {
            // Arrange
            var blockedUntil = By.XPath($"{TelevisionTrafficPanel}//p[starts-with(normalize-space(), 'Blocked until')]");
            Browser.Open(path: "/admin/settings/temporary");
            Browser.ActUntil(
                action: () => Browser.Driver.FindElement(_block).Click(),
                done: () => Browser.IsDisplayed(by: _unblock) && Browser.IsDisplayed(by: blockedUntil)
                    && !Browser.IsPresent(by: _block),
                because: "Block stops answering televisions, says until when, and offers Unblock now instead");

            // Act
            // Assert
            Browser.ActUntil(
                action: () => Browser.Driver.FindElement(_unblock).Click(),
                done: () => Browser.IsDisplayed(by: _block) && !Browser.IsPresent(by: _unblock)
                    && !Browser.IsPresent(by: blockedUntil),
                because: "unblocking answers televisions again and puts the panel back in its starting state");
        }

        private static void Restore(By button, string because)
        {
            Browser.Open(path: "/admin/settings/temporary");

            if (Browser.IsPresent(by: button))
            {
                Browser.ActUntil(
                    action: () => Browser.Driver.FindElement(button).Click(),
                    done: () => !Browser.IsPresent(by: button),
                    because: because);
            }
        }
    }
}
