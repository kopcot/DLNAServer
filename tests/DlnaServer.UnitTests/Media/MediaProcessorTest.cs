using DlnaServer.Core.Contracts.Processing;
using DlnaServer.Core.Dlna;
using DlnaServer.Media.Processing;
using DlnaServer.Media.Processing.Provisioning;
using DlnaServer.Media.Processing.Thumbnails;
using Microsoft.Extensions.Logging.Abstractions;
using Xabe.FFmpeg;

namespace DlnaServer.UnitTests.Media
{
    [TestFixture]
    internal sealed class MediaProcessorTest
    {
        private const string XabeStackTrace =
            "   at System.Collections.Generic.List`1.ToArray()\n"
            + "   at Xabe.FFmpeg.FFmpegWrapper.<>c__DisplayClass14_0.<RunProcess>b__0()";

        /// <summary>
        /// The shape logged on the NAS for VID_20250606_160533.mp4, whose next attempt three seconds later
        /// produced its preview without complaint.
        /// </summary>
        [Test]
        public void IsOutputLogRace_ForTheCopyFailingInsideXabe_IsTrue()
        {
            // Act
            var isRace = MediaProcessor.IsOutputLogRace("destinationArray", XabeStackTrace);

            // Assert
            isRace.Should().BeTrue(
                "because that is Xabe copying its output log while ffmpeg's last lines are still arriving");
        }

        [Test]
        public void IsOutputLogRace_ForTheSameParameterOutsideXabe_IsFalse()
        {
            // Act
            var isRace = MediaProcessor.IsOutputLogRace(
                "destinationArray",
                "   at System.Array.Copy(Array sourceArray, Array destinationArray, Int32 length)\n"
                    + "   at DlnaServer.Media.Processing.Thumbnails.ImageThumbnailGenerator.Generate()");

            // Assert
            isRace.Should().BeFalse(
                "because a copy failing in our own code is a defect to report, not a race to ride out");
        }

        [Test]
        public void IsOutputLogRace_ForAnotherArgumentInsideXabe_IsFalse()
        {
            // Act
            var isRace = MediaProcessor.IsOutputLogRace("path", XabeStackTrace);

            // Assert
            isRace.Should().BeFalse(
                "because only the output-log copy is known to fail after ffmpeg has finished its work");
        }

        [Test]
        public void IsOutputLogRace_WithoutAStackTrace_IsFalse()
        {
            // Act
            var isRace = MediaProcessor.IsOutputLogRace("destinationArray", stackTrace: null);

            // Assert
            isRace.Should().BeFalse("because an exception that was never thrown cannot have come from Xabe");
        }

        /// <summary>
        /// A probe that times out is a failed read, not a file with no streams.
        /// </summary>
        /// <remarks>
        /// It returned an empty result, so the caller saved it: the file's stored streams were wiped and it
        /// was stamped done with no failure recorded, and <c>MaxFailureCount</c> never saw it. The seam
        /// throws what the linked timeout token throws, while the caller's own token stays live.
        /// </remarks>
        [Test]
        public async Task ExtractMetadataAsync_WhenTheProbeTimesOut_ReportsAReadFailure()
        {
            // Arrange
            var processor = new MediaProcessor(
                new AvailableFFmpeg(),
                new ImageThumbnailGenerator(NullLogger<ImageThumbnailGenerator>.Instance),
                NullLogger<MediaProcessor>.Instance,
                getMediaInfo: static (_, _) => throw new OperationCanceledException());

            var settings = new MediaProcessingSettings(
                MaxWidth: 160,
                MaxHeight: 160,
                Quality: 80,
                ThumbnailMime: DlnaMime.ImageJpeg,
                StoreThumbnailContent: false,
                AllowFFmpegDownload: false,
                ReadContainerTags: false);

            // Act
            var metadata = await processor.ExtractMetadataAsync(
                filePath: Path.Combine(Path.GetTempPath(), "hung.mkv"),
                mime: DlnaMime.VideoXMatroska,
                settings: settings,
                cancellationToken: CancellationToken.None);

            // Assert
            metadata.Should().BeNull(
                "because a timed-out probe read nothing, and only null makes the caller record a failure "
                + "instead of saving an empty result over the file's streams");
        }

        [Test]
        public async Task ExtractMetadataAsync_ForAPlaylist_NeverProbesIt()
        {
            // Arrange
            var probes = new List<string>();
            var processor = ProcessorRecordingProbes(probes);

            // Act
            var metadata = await processor.ExtractMetadataAsync(
                filePath: Path.Combine(Path.GetTempPath(), "list.m3u"),
                mime: DlnaMime.AudioXMpequrl,
                settings: Settings(),
                cancellationToken: CancellationToken.None);

            // Assert
            metadata.Should().Be(MediaMetadataResult.Empty,
                "because an empty result records the attempt, so the file is not offered again");
            probes.Should().BeEmpty("because ffprobe would open every address the playlist names");
        }

        [Test]
        public async Task GenerateThumbnailAsync_ForAPlaylist_NeverProbesIt()
        {
            // Arrange
            var probes = new List<string>();
            var processor = ProcessorRecordingProbes(probes);

            // Act
            var thumbnail = await processor.GenerateThumbnailAsync(
                filePath: Path.Combine(Path.GetTempPath(), "list.m3u"),
                mime: DlnaMime.AudioXMpequrl,
                targetPath: Path.Combine(Path.GetTempPath(), $"dlna-{Guid.NewGuid():N}", "list.m3u.jpg"),
                settings: Settings(),
                allowAdoption: false,
                notOlderThanUtc: null,
                knownDuration: null,
                cancellationToken: CancellationToken.None);

            // Assert
            thumbnail.Should().BeNull("because a playlist has no picture");
            probes.Should().BeEmpty("because ffprobe would open every address the playlist names");
        }

        [Test]
        public void ContainerTagArguments_LimitsFFprobeToLocalFiles()
        {
            // Act
            var arguments = MediaProcessor.ContainerTagArguments("/media/film.mkv");

            // Assert
            arguments.Should().Contain("-protocol_whitelist file",
                "because a crafted container can name network addresses ffprobe would otherwise open");
            arguments.IndexOf("-protocol_whitelist", StringComparison.Ordinal).Should().BeLessThan(
                arguments.IndexOf("/media/film.mkv", StringComparison.Ordinal),
                "because ffprobe applies an input option only when it comes before the input");
        }

        private static MediaProcessor ProcessorRecordingProbes(List<string> probes)
        {
            return new MediaProcessor(
                new AvailableFFmpeg(),
                new ImageThumbnailGenerator(NullLogger<ImageThumbnailGenerator>.Instance),
                NullLogger<MediaProcessor>.Instance,
                getMediaInfo: (path, _) =>
                {
                    probes.Add(path);

                    throw new InvalidOperationException("No probe is expected");
                });
        }

        private static MediaProcessingSettings Settings()
        {
            return new MediaProcessingSettings(
                MaxWidth: 160,
                MaxHeight: 160,
                Quality: 80,
                ThumbnailMime: DlnaMime.ImageJpeg,
                StoreThumbnailContent: false,
                AllowFFmpegDownload: false,
                ReadContainerTags: true);
        }

        private sealed class AvailableFFmpeg : IFFmpegProvisioner
        {
            public Task<bool> EnsureAvailableAsync(bool allowDownload, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(true);
            }
        }
    }
}