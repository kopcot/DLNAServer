namespace DlnaServer.UiTests
{
    /// <summary>
    /// One Host and one browser for the whole run: starting either costs seconds. A test that changes
    /// shared state either changes what no other test reads - the saved model name - or puts it back in its
    /// tear-down - the hidden-folder switch.
    /// </summary>
    [SetUpFixture]
    internal sealed class UiTestSetup
    {
        public static AdminHost Host { get; private set; } = null!;

        public static AdminBrowser Browser { get; private set; } = null!;

        [OneTimeSetUp]
        public async Task StartAsync()
        {
            Host = await AdminHost.StartAsync(cancellationToken: CancellationToken.None);
            Browser = new AdminBrowser(host: Host);
        }

        [OneTimeTearDown]
        public async Task StopAsync()
        {
            try
            {
                Browser?.Dispose();
            }
            finally
            {
                if (Host is not null)
                {
                    await Host.DisposeAsync();
                }
            }
        }
    }
}
