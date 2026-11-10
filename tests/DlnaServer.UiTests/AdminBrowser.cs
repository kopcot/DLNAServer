using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace DlnaServer.UiTests
{
    /// <summary>
    /// One headless Chrome on the admin port. On a CI runner it uses the runner's own chromedriver; elsewhere
    /// Selenium Manager finds or fetches a matching one.
    /// </summary>
    /// <remarks>
    /// Selectors are tags, ids, hrefs and visible text only - never a CSS class - so restyling the admin
    /// pages cannot break a test that the pages themselves still pass.
    /// </remarks>
    internal sealed class AdminBrowser : IDisposable
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan _attemptTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(250);

        // ponytail: nothing signals that a circuit survived its first interactive render, so a failure is
        // given this long to reach the console; raise it if a slow runner lets one through.
        private static readonly TimeSpan _circuitSettle = TimeSpan.FromSeconds(1);

        private readonly AdminHost _host;

        // The current test's console, kept because reading the browser's log empties it.
        private readonly List<LogEntry> _console = [];

        private int _consoleAtLastOpen;

        public AdminBrowser(AdminHost host)
        {
            _host = host;

            // Selenium Manager otherwise reports every run to a third-party statistics service.
            Environment.SetEnvironmentVariable("SE_AVOID_STATS", "true");

            var options = new ChromeOptions();

            // Desktop width keeps the navigation visible; the phone layout folds it behind a menu button.
            options.AddArguments("--headless=new", "--window-size=1366,900");

            // Ubuntu's user-namespace restriction breaks the sandbox on runners, and only loopback is visited.
            if (OperatingSystem.IsLinux())
            {
                options.AddArgument("--no-sandbox");
            }

            // The console is where a Blazor circuit reports that it died.
            options.SetLoggingPreference(LogType.Browser, LogLevel.All);

            // GitHub's runner images carry a chromedriver matched to their Chrome; nothing is downloaded then.
            var runnerDriverFolder = Environment.GetEnvironmentVariable("CHROMEWEBDRIVER");
            var service = string.IsNullOrEmpty(runnerDriverFolder)
                ? ChromeDriverService.CreateDefaultService()
                : ChromeDriverService.CreateDefaultService(runnerDriverFolder);
            Driver = new ChromeDriver(service, options);
        }

        public IWebDriver Driver { get; }

        /// <summary>
        /// A link inside <c>main</c> to a file's preview page whose text contains <paramref name="name"/>.
        /// </summary>
        public static By PreviewLink(string name)
        {
            return By.XPath($"//main//a[starts-with(@href,'/admin/preview/') and contains(normalize-space(),'{name}')]");
        }

        public void Open(string path)
        {
            ReadConsole();
            _consoleAtLastOpen = _console.Count;
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

        /// <summary>
        /// Whether anything matches - for asserting that something is absent.
        /// </summary>
        public bool IsPresent(By by)
        {
            return Driver.FindElements(by).Count > 0;
        }

        /// <summary>
        /// Whether a match is actually shown - for asserting that something is there for the operator.
        /// </summary>
        public bool IsDisplayed(By by)
        {
            return Driver.FindElements(by).Any(static e => e.Displayed);
        }

        /// <summary>
        /// Waits until <paramref name="condition"/> holds, failing with <paramref name="because"/> if it never does.
        /// </summary>
        public void WaitUntil(Func<bool> condition, string because)
        {
            var wait = Wait();
            wait.Message = because;
            _ = wait.Until(_ => condition());
        }

        /// <summary>
        /// Repeats <paramref name="action"/> until <paramref name="done"/> holds, failing with
        /// <paramref name="because"/> if it never does.
        /// </summary>
        /// <remarks>
        /// A page is prerendered and then attaches its Blazor circuit; until it has, a click does nothing and
        /// typed text is discarded by the first interactive render. Repeating the whole action, re-finding
        /// every element, rides that out without a sleep, and a stale element from a re-render is not fatal.
        /// </remarks>
        public void ActUntil(Action action, Func<bool> done, string because)
        {
            var attempt = new WebDriverWait(Driver, _attemptTimeout);
            attempt.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));
            var deadline = DateTime.UtcNow + _timeout;

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

                    _ = attempt.Until(_ => done());

                    return;
                }
                catch (WebDriverException exception) when (exception is StaleElementReferenceException
                    or NoSuchElementException
                    or ElementClickInterceptedException
                    or WebDriverTimeoutException)
                {
                    if (DateTime.UtcNow >= deadline)
                    {
                        throw new WebDriverTimeoutException($"Not done within {_timeout.TotalSeconds} seconds: {because}", exception);
                    }

                    // Not interactive yet, or the element was re-rendered under the action: go round again.
                    Thread.Sleep(_retryDelay);
                }
            }
        }

        /// <summary>
        /// Waits for the circuit of the page last opened to connect, gives it a moment to fail, and fails on any
        /// console error.
        /// </summary>
        /// <remarks>
        /// The layout's error boundary is static, so it only catches a page that throws while prerendering;
        /// a throw in the interactive render ends the circuit and leaves the prerendered page looking fine.
        /// </remarks>
        public void AssertCircuitWithoutError()
        {
            var wait = Wait();
            wait.Message = "because an interactive page connects its circuit once it has loaded";
            _ = wait.Until(_ =>
            {
                ReadConsole();

                return _console.Skip(_consoleAtLastOpen).Any(static e => e.Level == LogLevel.Severe
                    || e.Message.Contains("WebSocket connected", StringComparison.Ordinal));
            });

            Thread.Sleep(_circuitSettle);
            ReadConsole();

            ConsoleErrors().Should().BeEmpty(
                "because an error in the console means the page's circuit died and its controls stopped working");
        }

        /// <summary>
        /// Run from every fixture's tear-down: fails a passing test whose page logged an error, and attaches a
        /// screenshot to a failed test so a red CI run shows what the page looked like.
        /// </summary>
        public void CheckAfterTest()
        {
            ReadConsole();
            var errors = ConsoleErrors();
            _console.Clear();
            _consoleAtLastOpen = 0;

            var hasFailed = TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed;

            if (hasFailed || errors.Count > 0)
            {
                var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{TestContext.CurrentContext.Test.ID}.png");
                ((ITakesScreenshot)Driver).GetScreenshot().SaveAsFile(path);
                TestContext.AddTestAttachment(path);
            }

            // A failure in a tear-down replaces the test's own message, so a test already red keeps that one.
            if (!hasFailed)
            {
                errors.Should().BeEmpty(
                    "because an error in the console means a page broke while the test drove it");
            }
        }

        public void Dispose()
        {
            Driver.Quit();
        }

        private void ReadConsole()
        {
            _console.AddRange(Driver.Manage().Logs.GetLog(LogType.Browser));
        }

        private List<string> ConsoleErrors()
        {
            return _console
                .Where(static e => e.Level == LogLevel.Severe)
                .Select(static e => e.Message)
                .ToList();
        }

        private WebDriverWait Wait()
        {
            var wait = new WebDriverWait(Driver, _timeout);
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));

            return wait;
        }
    }
}
