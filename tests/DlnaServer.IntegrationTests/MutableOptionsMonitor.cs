using Microsoft.Extensions.Options;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// An <see cref="IOptionsMonitor{TOptions}"/> whose value can be replaced, for tests that exercise a
    /// component's reaction to a configuration change without going through the file system.
    /// </summary>
    internal sealed class MutableOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
    {
        public MutableOptionsMonitor(TOptions value)
        {
            CurrentValue = value;
        }

        public TOptions CurrentValue { get; set; }

        public TOptions Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable? OnChange(Action<TOptions, string?> listener)
        {
            return null;
        }
    }
}
