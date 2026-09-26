using DlnaServer.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Configuration
{
    /// <summary>
    /// Serves the last options that validated when the current ones do not, instead of throwing at
    /// every reader for the rest of the process.
    /// </summary>
    /// <remarks>
    /// <c>OptionsCache.GetOrAdd</c> stores a <see cref="Lazy{T}"/> built with the default
    /// <c>LazyThreadSafetyMode.ExecutionAndPublication</c>, and that mode <b>caches the exception and
    /// rethrows the same instance on every later access</b>. So one parseable-but-invalid edit -
    /// <c>"Port": 0</c>, a relative source folder, a per-file cache limit above the total - made every
    /// subsequent <c>CurrentValue</c> read throw until <c>config.json</c> changed again.
    /// <para>
    /// That is read on every media request, every listing, every Browse and every executed database
    /// command, so the whole server answered 500 - <b>including the Settings page that would have
    /// repaired the edit</b>, which left an operator with no way back except SSH. Two background
    /// services died with it, silently, because <c>BackgroundServiceExceptionBehavior.Ignore</c>
    /// swallows what escapes their loop.
    /// </para>
    /// <para>
    /// Startup is deliberately NOT protected: nothing has validated yet, so there is no last good value
    /// and the exception propagates exactly as before. A configuration that has never been usable still
    /// refuses to boot, which is what <c>ValidateOnStart</c> is for.
    /// </para>
    /// </remarks>
    internal sealed partial class LastGoodDlnaOptionsMonitor : IOptionsMonitor<DlnaOptions>
    {
        private static readonly string? _binderAssemblyName = typeof(ConfigurationBinder).Assembly.GetName().Name;

        private readonly IOptionsMonitor<DlnaOptions> _inner;
        private readonly ILogger<LastGoodDlnaOptionsMonitor> _logger;

        private DlnaOptions? _lastGood;
        private volatile bool _isCurrentValid = true;

        public LastGoodDlnaOptionsMonitor(
            IOptionsMonitor<DlnaOptions> inner,
            ILogger<LastGoodDlnaOptionsMonitor> logger)
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(logger);

            _inner = inner;
            _logger = logger;
        }

        public DlnaOptions CurrentValue => Get(Options.DefaultName);

        /// <summary>
        /// Whether the most recent read validated, rather than falling back to the last good values.
        /// </summary>
        /// <remarks>
        /// Says nothing on its own: it reports the outcome of the last <see cref="Get"/>, so a caller that
        /// needs the state NOW - the readiness probe - reads <see cref="CurrentValue"/> first, which is
        /// what re-runs validation. Starts true because <c>ValidateOnStart</c> has already passed by the
        /// time anything can ask.
        /// </remarks>
        public bool IsCurrentValid => _isCurrentValid;

        public DlnaOptions Get(string? name)
        {
            try
            {
                var current = _inner.Get(name);

                // Only a value that came back without throwing is worth keeping, and reference
                // assignment is atomic - a concurrent reader sees either the previous instance or this
                // one, never a half-built graph. The options object is treated as immutable by every
                // consumer, so sharing one instance across threads is what already happens.
                _lastGood = current;
                _isCurrentValid = true;

                return current;
            }
            catch (OptionsValidationException exception) when (_lastGood is not null)
            {
                // Deliberately not caching the failure or rate-limiting the message. Every read that
                // would have thrown logs one line, which is loud - and that is the point: the server is
                // running on settings the operator has since edited, and the noise stops the moment the
                // file is valid again.
                LogServingLastGood(string.Join(" ", exception.Failures));
                _isCurrentValid = false;

                return _lastGood;
            }
            catch (InvalidOperationException exception) when (_lastGood is not null && IsBindingFailure(exception))
            {
                // A value of the wrong type - "DebugMode": "yes" - fails in the binder before validation
                // runs, and OptionsCache caches that exception exactly as it caches a validation failure.
                LogServingLastGoodAfterBindingFailure(exception.Message);
                _isCurrentValid = false;

                return _lastGood;
            }
        }

        public IDisposable? OnChange(Action<DlnaOptions, string?> listener)
        {
            return _inner.OnChange(listener);
        }

        /// <summary>
        /// Whether the exception was thrown by <see cref="ConfigurationBinder"/> itself.
        /// </summary>
        /// <remarks>
        /// Only the binder's own failures are an edit to <c>config.json</c> - "failed to convert", "cannot
        /// create an instance". An <see cref="InvalidOperationException"/> from anywhere else - a
        /// <c>PostConfigure</c> step, the container - is a defect, and serving stale settings would hide it.
        /// <see cref="Exception.Source"/> names the assembly of the method that threw, which survives the
        /// rethrow that <c>OptionsCache</c>'s cached <see cref="Lazy{T}"/> performs.
        /// </remarks>
        private static bool IsBindingFailure(InvalidOperationException exception)
        {
            return string.Equals(exception.Source, _binderAssemblyName, StringComparison.Ordinal);
        }
    }
}
