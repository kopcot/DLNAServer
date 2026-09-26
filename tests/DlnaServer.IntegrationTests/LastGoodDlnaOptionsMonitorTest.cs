using DlnaServer.Core.Configuration;
using DlnaServer.Host.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the monitor that keeps the server on its last valid settings when <c>config.json</c> is
    /// edited into something unusable at runtime.
    /// </summary>
    /// <remarks>
    /// Wired through the real options pipeline - binder, validation and <c>OptionsCache</c> - because the
    /// defect this guards against lives in that pipeline: a failure is cached and rethrown to every reader
    /// until the file changes again.
    /// </remarks>
    [TestFixture]
    internal sealed class LastGoodDlnaOptionsMonitorTest
    {
        private const string PortKey = "Dlna:Server:Port";
        private const string DebugModeKey = "Dlna:Server:DebugMode";

        private IConfigurationRoot _configuration = null!;
        private ServiceProvider _services = null!;
        private LastGoodDlnaOptionsMonitor _monitor = null!;

        [SetUp]
        public void SetUp()
        {
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [PortKey] = "26852",
                    [DebugModeKey] = "false",
                })
                .Build();

            var collection = new ServiceCollection();
            _ = collection
                .AddOptions<DlnaOptions>()
                .Bind(_configuration.GetSection(DlnaOptions.SectionName))
                .Validate(static o => o.Server.Port != 0, "Port must not be 0.");

            _services = collection.BuildServiceProvider();
            _monitor = new LastGoodDlnaOptionsMonitor(
                _services.GetRequiredService<IOptionsMonitor<DlnaOptions>>(),
                NullLogger<LastGoodDlnaOptionsMonitor>.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            _services.Dispose();
        }

        /// <summary>
        /// <b>Reproduced live.</b> <c>"DebugMode": "yes"</c> made <c>/health/ready</c> answer 500 and
        /// tripped the admin UI's error boundary, because the binder's conversion failure is an
        /// <see cref="InvalidOperationException"/> and only a validation failure was caught.
        /// </summary>
        [Test]
        public void CurrentValue_WhenAValueOfTheWrongTypeFollowsAGoodOne_ServesTheLastGood()
        {
            // Arrange
            var good = _monitor.CurrentValue;
            Edit(key: DebugModeKey, value: "yes");

            // Act
            var served = _monitor.CurrentValue;

            // Assert
            served.Should().BeSameAs(good,
                "because a value the binder cannot convert must not take every reader of the settings down");
            _monitor.IsCurrentValid.Should().BeFalse(
                "because readiness has to report that the running settings no longer match the file");
        }

        [Test]
        public void CurrentValue_WhenAValidationFailureFollowsAGoodOne_ServesTheLastGood()
        {
            // Arrange
            var good = _monitor.CurrentValue;
            Edit(key: PortKey, value: "0");

            // Act
            var served = _monitor.CurrentValue;

            // Assert
            served.Should().BeSameAs(good,
                "because a parseable but invalid edit is the case the monitor was written for");
            _monitor.IsCurrentValid.Should().BeFalse(
                "because readiness has to report that the running settings no longer match the file");
        }

        [Test]
        public void CurrentValue_OnceTheFileIsFixed_ServesTheNewValuesAndIsValidAgain()
        {
            // Arrange
            _ = _monitor.CurrentValue;
            Edit(key: DebugModeKey, value: "yes");
            _ = _monitor.CurrentValue;
            Edit(key: DebugModeKey, value: "true");

            // Act
            var served = _monitor.CurrentValue;

            // Assert
            served.Server.DebugMode.Should().BeTrue("because the corrected file is what the server runs on now");
            _monitor.IsCurrentValid.Should().BeTrue("because the current file binds and validates again");
        }

        /// <summary>
        /// A configuration that has never been usable still refuses to boot.
        /// </summary>
        [Test]
        public void CurrentValue_WithAWrongTypeAndNoLastGood_Throws()
        {
            // Arrange
            Edit(key: DebugModeKey, value: "yes");

            // Act
            var read = () => _monitor.CurrentValue;

            // Assert
            read.Should().Throw<InvalidOperationException>(
                "because with nothing valid to fall back to, startup has to fail rather than run on defaults");
        }

        private void Edit(string key, string value)
        {
            _configuration[key] = value;

            // Raises the change token, which is what makes OptionsMonitor drop its cached instance. Its
            // change callback re-reads at once, so an unusable value throws here as well - on the server
            // that is the file watcher's callback. The failure it caches is what the tests then read.
            try
            {
                _configuration.Reload();
            }
            catch (AggregateException)
            {
            }
        }
    }
}
