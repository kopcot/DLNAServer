using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Processing;
using DlnaServer.Core.Dlna;
using DlnaServer.Media.Processing;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// An <see cref="IMediaProcessor"/> that throws for the paths a test nominates and succeeds for the
    /// rest, standing in for a corrupt image or an unreadable container.
    /// </summary>
    /// <remarks>
    /// It throws <see cref="InvalidDataException"/> deliberately: the real failures are Skia's and
    /// Xabe's own types, and the point of the guard is that it does not depend on knowing which.
    /// </remarks>
    internal sealed class ThrowingMediaProcessor : IMediaProcessor
    {
        private readonly HashSet<string> _failingPaths;

        public ThrowingMediaProcessor(params string[] failingPaths)
        {
            _failingPaths = new HashSet<string>(failingPaths, StringComparer.Ordinal);
        }

        public Task<MediaMetadataResult?> ExtractMetadataAsync(
            string filePath,
            DlnaMime mime,
            MediaProcessingSettings settings,
            CancellationToken cancellationToken = default)
        {
            if (_failingPaths.Contains(filePath))
            {
                throw new InvalidDataException($"'{filePath}' cannot be probed");
            }

            return Task.FromResult<MediaMetadataResult?>(MediaMetadataResult.Empty);
        }

        public Task<GeneratedThumbnail?> GenerateThumbnailAsync(
            string filePath,
            DlnaMime mime,
            string targetPath,
            MediaProcessingSettings settings,
            bool allowAdoption = true,
            DateTime? notOlderThanUtc = null,
            TimeSpan? knownDuration = null,
            CancellationToken cancellationToken = default)
        {
            if (_failingPaths.Contains(filePath))
            {
                throw new InvalidDataException($"'{filePath}' cannot be thumbnailed");
            }

            return Task.FromResult<GeneratedThumbnail?>(
                new GeneratedThumbnail(targetPath, DlnaMime.ImageJpeg, 16, 16, 1024, null, WasAdopted: false));
        }
    }
}
