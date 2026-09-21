using DlnaServer.Core.Diagnostics;

namespace DlnaServer.UnitTests
{
    /// <summary>
    /// An <see cref="ITemporaryFolderVisibility"/> whose two lists are handed in, for tests about what a
    /// repository does with them.
    /// </summary>
    /// <remarks>
    /// Deliberately holds the answers rather than composing them. Composing here would be a second copy
    /// of the rule, free to drift from the real one and to keep passing while it did - which is the exact
    /// failure the single <c>HiddenPathQuery</c> exists to prevent. The real composition, the window and
    /// its expiry are covered against the real type in <c>TemporaryFolderVisibilityTest</c>.
    /// </remarks>
    internal sealed class FixedFolderVisibility : ITemporaryFolderVisibility
    {
        public FixedFolderVisibility(IList<string> hiddenFromListings, IList<string> hiddenFromDelivery)
        {
            HiddenFromListings = hiddenFromListings;
            HiddenFromDelivery = hiddenFromDelivery;
        }

        public bool IsVisible => HiddenFromDelivery.Count == 0;

        public DateTimeOffset? VisibleUntilUtc => null;

        public IList<string> HiddenFromListings { get; }

        public IList<string> HiddenFromDelivery { get; }

        public void ShowFor(TimeSpan duration)
        {
            throw new NotSupportedException("The lists are fixed for the life of this instance.");
        }

        public void Hide()
        {
            throw new NotSupportedException("The lists are fixed for the life of this instance.");
        }
    }
}
