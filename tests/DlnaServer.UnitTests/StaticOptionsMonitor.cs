using Microsoft.Extensions.Options;

namespace DlnaServer.UnitTests
{
    /// <summary>
    /// An <see cref="IOptionsMonitor{TOptions}"/> over one fixed instance, for tests that do not exercise reload.
    /// </summary>
    internal sealed class StaticOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
    {
        public StaticOptionsMonitor(TOptions value)
        {
            CurrentValue = value;
        }

        public TOptions CurrentValue { get; }

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
