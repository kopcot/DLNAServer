using DlnaServer.Core.Subtitles;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// An <see cref="ISubtitleFileChecker"/> that accepts every path as given, without looking at the disc -
    /// which is how a test stands in for a file deleted between being checked and being read.
    /// </summary>
    internal sealed class AcceptingSubtitleFileChecker : ISubtitleFileChecker
    {
        public bool TryCheck(string mediaDirectory, string? input, out string relativePath, out string problem)
        {
            relativePath = input ?? string.Empty;
            problem = string.Empty;

            return true;
        }
    }
}
