using DlnaServer.Core.Diagnostics;
using DlnaServer.Core.Subtitles;

namespace DlnaServer.Host.Configuration
{
    /// <inheritdoc cref="ISubtitleFileChecker"/>
    internal sealed class SubtitleFileChecker : ISubtitleFileChecker
    {
        private readonly ITemporaryFolderVisibility _visibility;

        public SubtitleFileChecker(ITemporaryFolderVisibility visibility)
        {
            _visibility = visibility;
        }

        /// <remarks>
        /// Against what the library hides from listings, which is <c>ExcludeFolders</c> plus the temporarily
        /// hidden folders - so a temporarily hidden folder is accepted while it is being shown.
        /// </remarks>
        public bool TryCheck(string mediaDirectory, string? input, out string relativePath, out string problem)
        {
            try
            {
                return SubtitlePath.TryLocate(
                    mediaDirectory,
                    input,
                    [.. _visibility.HiddenFromListings],
                    out relativePath,
                    out _,
                    out problem);
            }
            catch (Exception exception)
                when (exception is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // A name the filesystem refuses, or a share that stops answering, is a refusal for the page to
                // show - not something to end the operator's session over.
                relativePath = string.Empty;
                problem = "That path cannot be read on this server.";

                return false;
            }
        }
    }
}
