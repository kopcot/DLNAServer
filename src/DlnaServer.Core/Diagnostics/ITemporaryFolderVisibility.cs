using DlnaServer.Core.Configuration;

namespace DlnaServer.Core.Diagnostics
{
    /// <summary>
    /// Reveals <see cref="LibraryOptions.TemporarilyHiddenFolders"/> for a period, and answers what is
    /// hidden right now.
    /// </summary>
    /// <remarks>
    /// The window is state with an expiry rather than a saved setting: it changes far too often to be
    /// worth writing and backing up <c>config.json</c> each time, and it is meant to lapse rather than to
    /// be remembered. A restart therefore hides everything again, which is the safe direction.
    /// <para>
    /// The two lists below are the reason this is not simply a boolean. Composing "what is hidden" at each
    /// call site is how the three hand-synced copies of the exclusion rule drifted apart before M1, so the
    /// composition lives here, once, and the repositories are handed the answer.
    /// </para>
    /// <para>
    /// Both are read from the options monitor on every access rather than cached off its change token.
    /// The monitor in front of this one serves the last options that <i>validated</i>, while its
    /// <c>OnChange</c> forwards whatever was last bound - so a cache built on the notification would
    /// follow a configuration the rest of the server has rejected, and hide by rules nothing else agrees
    /// with. An ordinary library allocates nothing here anyway.
    /// </para>
    /// </remarks>
    public interface ITemporaryFolderVisibility
    {
        /// <summary>
        /// True while the temporarily hidden folders are being shown.
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// When the current window lapses, or null when nothing is being shown.
        /// </summary>
        DateTimeOffset? VisibleUntilUtc { get; }

        /// <summary>
        /// Everything hidden from listings: the excluded folders always, and the temporarily hidden ones
        /// while the window is shut.
        /// </summary>
        IList<string> HiddenFromListings { get; }

        /// <summary>
        /// Everything hidden from delivery by identifier: the temporarily hidden folders while the window
        /// is shut, and nothing else.
        /// </summary>
        /// <remarks>
        /// Excluded folders are deliberately absent. Delivery has always been exempt from them so a
        /// renderer mid-stream is not cut off, and this must not change that.
        /// </remarks>
        IList<string> HiddenFromDelivery { get; }

        /// <summary>
        /// Shows the temporarily hidden folders for the given period, replacing any window already open.
        /// </summary>
        void ShowFor(TimeSpan duration);

        /// <summary>
        /// Hides them again immediately.
        /// </summary>
        void Hide();
    }
}
