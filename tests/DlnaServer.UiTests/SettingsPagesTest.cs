using OpenQA.Selenium;

namespace DlnaServer.UiTests
{
    [TestFixture]
    internal sealed class SettingsPagesTest
    {
        private static AdminBrowser Browser => UiTestSetup.Browser;

        [TearDown]
        public void TearDown()
        {
            Browser.AttachScreenshotOnFailure();
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
            Browser.Open("/admin/settings");

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
                    && Browser.Driver.FindElement(modelInput).GetDomProperty("value") == modelName);
            Browser.Open("/admin/settings");
            var reloaded = Browser.Read(d => d.FindElement(modelInput).GetDomProperty("value"));
            Browser.Open("/admin/about");
            var shown = Browser.TextOf(By.XPath("//dt[normalize-space()='Model']/following-sibling::dd[1]"));

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
            const string panel = "//h2[normalize-space()='Show temporarily hidden folders']/..";
            var show = By.XPath($"{panel}//button[normalize-space()='Show now']");
            var hide = By.XPath($"{panel}//button[normalize-space()='Hide now']");
            Browser.Open("/admin/settings/temporary");

            // Act
            Browser.ActUntil(action: () => Browser.Driver.FindElement(show).Click(), done: () => Browser.IsPresent(hide));
            Browser.ActUntil(action: () => Browser.Driver.FindElement(hide).Click(), done: () => Browser.IsPresent(show));

            // Assert
            Browser.IsPresent(hide).Should().BeFalse(
                "because hiding the folders again puts the panel back in its starting state");
        }
    }
}
