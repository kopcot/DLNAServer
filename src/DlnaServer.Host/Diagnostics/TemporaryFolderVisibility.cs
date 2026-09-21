using DlnaServer.Core.Configuration;
using DlnaServer.Core.Diagnostics;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Diagnostics
{
    /// <inheritdoc cref="ITemporaryFolderVisibility"/>
    internal sealed class TemporaryFolderVisibility : ITemporaryFolderVisibility
    {
        private static readonly string[] _nothing = [];

        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// The window, as a single reference so a reader never sees an expiry that has already been
        /// replaced.
        /// </summary>
        private volatile OpenWindow? _window;

        public TemporaryFolderVisibility(IOptionsMonitor<DlnaOptions> options, TimeProvider timeProvider)
        {
            _options = options;
            _timeProvider = timeProvider;
        }

        public bool IsVisible => Current() is not null;

        public DateTimeOffset? VisibleUntilUtc => Current()?.UntilUtc;

        public IList<string> HiddenFromListings
        {
            get
            {
                var library = _options.CurrentValue.Library;

                // The common case is no temporarily hidden folders at all, and then the answer is the
                // list the options object already holds - so an ordinary library allocates nothing here.
                if (IsVisible || library.TemporarilyHiddenFolders.Count == 0)
                {
                    return library.ExcludeFolders;
                }

                var combined = new List<string>(
                    library.ExcludeFolders.Count + library.TemporarilyHiddenFolders.Count);

                combined.AddRange(library.ExcludeFolders);
                combined.AddRange(library.TemporarilyHiddenFolders);

                return combined;
            }
        }

        public IList<string> HiddenFromDelivery =>
            IsVisible ? _nothing : _options.CurrentValue.Library.TemporarilyHiddenFolders;

        public void ShowFor(TimeSpan duration)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration.Ticks);

            _window = new OpenWindow(_timeProvider.GetUtcNow().Add(duration));
        }

        public void Hide()
        {
            _window = null;
        }

        /// <summary>
        /// The window if it is still open, clearing it once it has lapsed.
        /// </summary>
        private OpenWindow? Current()
        {
            var window = _window;

            if (window is null)
            {
                return null;
            }

            if (_timeProvider.GetUtcNow() < window.UntilUtc)
            {
                return window;
            }

            // Lapsed. Cleared here rather than by a timer, so nothing has to be scheduled or disposed -
            // the folders simply stop being visible the next time anything asks.
            _window = null;

            return null;
        }

        private sealed record OpenWindow(DateTimeOffset UntilUtc);
    }
}
