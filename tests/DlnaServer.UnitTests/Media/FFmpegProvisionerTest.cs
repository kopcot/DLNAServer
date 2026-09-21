using DlnaServer.Media.Processing.Provisioning;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.UnitTests.Media
{
    /// <summary>
    /// Covers the read-only capability report the admin UI renders, not the download itself.
    /// </summary>
    /// <remarks>
    /// The three states are the point. Availability resolves on first use, so a server that has not
    /// processed anything yet knows nothing - and rendering that as "unavailable" would put a false alarm
    /// on the dashboard of every freshly started server.
    /// </remarks>
    [TestFixture]
    internal sealed class FFmpegProvisionerTest
    {
        [Test]
        public void IsVideoProcessingAvailable_BeforeAnythingNeedsFFmpeg_IsUnknown()
        {
            // Arrange
            using var provisioner = new FFmpegProvisioner(NullLogger<FFmpegProvisioner>.Instance);

            // Assert
            provisioner.IsVideoProcessingAvailable.Should().BeNull(
                "because nothing has asked for ffmpeg yet, and reporting that as unavailable would warn "
                + "about a capability that may well be there");
        }

        /// <summary>
        /// The property must report what was resolved rather than re-deciding, because the dashboard reads
        /// it long after the processing pass that settled it.
        /// </summary>
        /// <remarks>
        /// Asserted against the returned value rather than against <c>false</c>: this runs from the test
        /// output folder, and whether an <c>ffmpeg</c> directory happens to sit beside it is not something
        /// the test can control. The invariant - that the two agree - is what the dashboard depends on and
        /// holds either way.
        /// </remarks>
        [Test]
        public async Task IsVideoProcessingAvailable_AfterResolution_MatchesWhatWasResolved()
        {
            // Arrange
            using var provisioner = new FFmpegProvisioner(NullLogger<FFmpegProvisioner>.Instance);

            // Act
            var resolved = await provisioner.EnsureAvailableAsync(
                allowDownload: false,
                cancellationToken: CancellationToken.None);

            // Assert
            provisioner.IsVideoProcessingAvailable.Should().Be(resolved,
                "because the capability report is the cached answer, and a second opinion would let the "
                + "dashboard and the processing pass disagree about the same deployment");
        }

        /// <summary>
        /// Reading the report must never start a download, or opening a page would fetch a third-party
        /// binary as a side effect of rendering.
        /// </summary>
        [Test]
        public void IsVideoProcessingAvailable_DoesNotResolveOnRead()
        {
            // Arrange
            using var provisioner = new FFmpegProvisioner(NullLogger<FFmpegProvisioner>.Instance);

            // Act
            _ = provisioner.IsVideoProcessingAvailable;
            _ = provisioner.IsVideoProcessingAvailable;

            // Assert
            provisioner.IsVideoProcessingAvailable.Should().BeNull(
                "because a read is a read - resolution belongs to EnsureAvailableAsync, which is the only "
                + "caller that has been told whether downloading is allowed");
        }
    }
}
