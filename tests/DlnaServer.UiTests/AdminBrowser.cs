using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace DlnaServer.UiTests
{
    /// <summary>
    /// One headless Chrome on the admin port. Selenium Manager finds or fetches a matching driver.
    /// </summary>
    /// <remarks>
    /// Selectors are tags, ids, hrefs and visible text only - never a CSS class - so restyling the admin
    /// pages cannot break a test that the pages themselves still pass.
    /// </remarks>
    internal sealed class AdminBrowser : IDisposable
    {
        private static readonly TimeSpan _wait = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan _retryInterval = TimeSpan.FromSeconds(1);

        private readonly AdminHost _host;

        public AdminBrowser(AdminHost host)
        {
            _host = host;

            var options = new ChromeOptions();

            // Desktop width keeps the navigation visible; the phone layout folds it behind a menu button.
            // No sandbox: Ubuntu's user-namespace restriction breaks it on runners, and only loopback is visited.
            options.AddArguments("--headless=new", "--window-size=1366,900", "--no-sandbox");
            Driver = new ChromeDriver(options);
        }

        public IWebDriver Driver { get; }

        public void Open(string path)
        {
            Driver.Navigate().GoToUrl(_host.Admin(path));
        }

        public IWebElement WaitFor(By by)
        {
            return Wait().Until(d => d.FindElement(by));
        }

        /// <summary>
        /// The text of the first match, re-found if the circuit's first interactive render replaced it.
        /// </summary>
        public string TextOf(By by)
        {
            return Read(d => d.FindElement(by).Text);
        }

        /// <summary>
        /// Waits for <paramref name="read"/> to return a value that is not null or false, reading again when
        /// a re-render left it holding a stale element.
        /// </summary>
        public T Read<T>(Func<IWebDriver, T> read)
        {
            return Wait().Until(read);
        }

        public bool IsPresent(By by)
        {
            return Driver.FindElements(by).Count > 0;
        }

        /// <summary>
        /// Repeats <paramref name="action"/> until <paramref name="done"/> holds.
        /// </summary>
        /// <remarks>
        /// A page is prerendered and then attaches its Blazor circuit; until it has, a click does nothing and
        /// typed text is discarded by the first interactive render. Repeating the whole action, re-finding
        /// every element, rides that out without a sleep, and a stale element from a re-render is not fatal.
        /// </remarks>
        public void ActUntil(Action action, Func<bool> done)
        {
            var wait = new WebDriverWait(Driver, _retryInterval);
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));
            var deadline = DateTime.UtcNow + _wait;

            while (true)
            {
                try
                {
                    // Checked first: a slow re-render can leave the last attempt's effect landing late, and the
                    // element the action needs is then already gone.
                    if (!done())
                    {
                        action();
                    }

                    _ = wait.Until(_ => done());

                    return;
                }
                catch (WebDriverException) when (DateTime.UtcNow < deadline)
                {
                    // Not interactive yet, or the element was re-rendered under the action - the wait's
                    // timeout is a WebDriverException too: go round again.
                }
            }
        }

        /// <summary>
        /// Attaches a screenshot to a failed test, so a red CI run shows what the page looked like.
        /// </summary>
        public void AttachScreenshotOnFailure()
        {
            if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
            {
                return;
            }

            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{TestContext.CurrentContext.Test.ID}.png");
            ((ITakesScreenshot)Driver).GetScreenshot().SaveAsFile(path);
            TestContext.AddTestAttachment(path);
        }

        public void Dispose()
        {
            Driver.Quit();
        }

        private WebDriverWait Wait()
        {
            var wait = new WebDriverWait(Driver, _wait);
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));

            return wait;
        }
    }
}
