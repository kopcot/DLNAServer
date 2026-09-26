using DlnaServer.Core.Diagnostics;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace DlnaServer.Media.Processing.Provisioning
{
    /// <summary>
    /// Locates ffmpeg and ffprobe, downloading them once if they are not present.
    /// </summary>
    /// <remarks>
    /// A positive answer is resolved once and cached; a negative one is retried, so binaries that arrive
    /// later are picked up without a restart. The reference called its equivalent at the top of every
    /// processing batch, so each batch re-enumerated the executables directory even once it knew.
    /// <para>
    /// A failure here is never fatal. Without ffmpeg the server still indexes, browses and streams; only
    /// video metadata and video thumbnails are unavailable, and that is reported once rather than as an
    /// error per file.
    /// </para>
    /// </remarks>
    internal sealed partial class FFmpegProvisioner : IFFmpegProvisioner, IMediaCapabilities, IDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly ILogger<FFmpegProvisioner> _logger;
        // A Nullable<bool> read outside the gate is two fields, so a second caller could observe
        // hasValue = true with value = false and conclude ffmpeg was unavailable - retiring every
        // in-flight file as a failure. Not latent: MediaProcessingHostedService runs two workers, and the
        // dashboard reads this too. Hence one int, read and written through Volatile - see the constants.
        private int _availability;

        private const int AvailabilityUnknown = 0;
        private const int AvailabilityYes = 1;
        private const int AvailabilityNo = 2;

        public FFmpegProvisioner(ILogger<FFmpegProvisioner> logger)
        {
            _logger = logger;
        }

        public bool? IsVideoProcessingAvailable => Volatile.Read(ref _availability) switch
        {
            AvailabilityYes => true,
            AvailabilityNo => false,
            _ => null,
        };

        private string? _lastUnavailableReason;

        public async Task<bool> EnsureAvailableAsync(bool allowDownload, CancellationToken cancellationToken = default)
        {
            // Only a positive answer is cached for the life of the process. A negative one used to be
            // too, which made the dashboard's own instructions impossible to follow: it tells the
            // operator to drop the binaries into the ffmpeg folder - or enable the download - and then
            // recreate metadata from Maintenance, but every reprocessed file was skipped against the
            // cached "no" and the notice never cleared. Re-resolving costs a directory probe on a path
            // that only runs when ffmpeg is absent, and absent is the state worth escaping.
            if (Volatile.Read(ref _availability) == AvailabilityYes)
            {
                return true;
            }

            await _gate.WaitAsync(cancellationToken);

            try
            {
                if (Volatile.Read(ref _availability) == AvailabilityYes)
                {
                    return true;
                }

                var isAvailable = await ResolveAsync(allowDownload, cancellationToken);
                Volatile.Write(ref _availability, isAvailable ? AvailabilityYes : AvailabilityNo);

                return isAvailable;
            }
            finally
            {
                _ = _gate.Release();
            }
        }

        public void Dispose()
        {
            _gate.Dispose();
        }

        /// <summary>
        /// Reports ffmpeg as unavailable once per distinct reason rather than once per attempt.
        /// </summary>
        /// <remarks>
        /// A negative answer is deliberately NOT cached - see <c>EnsureAvailableAsync</c>, which explains
        /// why re-resolving is what lets the dashboard's own instructions be followed without a restart.
        /// The consequence was a Warning per resolution, and the guard that calls it sits on four paths
        /// reached per file: on a 25,504-file library with ffmpeg absent that is tens of thousands of
        /// identical lines in app.log, which rolls the thing an operator needs to read straight out of it.
        /// <para>
        /// Called only from inside the gate, so the field needs no synchronisation of its own.
        /// </para>
        /// </remarks>
        private void ReportUnavailable(string reason)
        {
            if (string.Equals(_lastUnavailableReason, reason, StringComparison.Ordinal))
            {
                return;
            }

            _lastUnavailableReason = reason;
            LogUnavailable(reason);
        }

        private async Task<bool> ResolveAsync(bool allowDownload, CancellationToken cancellationToken)
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "ffmpeg");

            try
            {
                _ = Directory.CreateDirectory(directory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A read-only or full deployment folder. This sat outside every try, and the caller
                // checks availability before its own guard, so the throw reached the hosted service.
                ReportUnavailable(exception.Message);
                return false;
            }

            var executableSuffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
            var ffmpeg = Path.Combine(directory, "ffmpeg" + executableSuffix);
            var ffprobe = Path.Combine(directory, "ffprobe" + executableSuffix);

            if (File.Exists(ffmpeg) && File.Exists(ffprobe))
            {
                FFmpeg.SetExecutablesPath(directory);
                LogUsingExisting(directory);

                // Cleared so a later disappearance is reported again rather than silenced by the
                // de-duplication below.
                _lastUnavailableReason = null;

                return true;
            }

            if (!allowDownload)
            {
                ReportUnavailable("not present and downloading is disabled");
                return false;
            }

            try
            {
                // Xabe.FFmpeg.Downloader fetches ffbinaries.com / xabe.net over TLS and carries no hash,
                // checksum or signature - inspected on disc, not assumed - so nothing here can tell a
                // genuine archive from a substituted one, and "latest" pins no version. It is then
                // extracted, marked executable and run as the service account. The guard is that this
                // branch is unreachable unless config.json turns Thumbnails.DownloadFFmpeg on by hand:
                // no property initialiser sets it, DlnaOptionsDefaults never touches it, the validator
                // does not require it, and the Settings page exposes no control. Keep it that way.
                LogDownloading(directory);
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, directory);

                if (!File.Exists(ffmpeg) || !File.Exists(ffprobe))
                {
                    ReportUnavailable("the download completed but the executables are missing");
                    return false;
                }

                FFmpeg.SetExecutablesPath(directory);
                LogDownloaded(directory);

                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // A slow NAS link is the case that matters: the downloader's HttpClient throws
                // TaskCanceledException on its own timeout, not HttpRequestException, so the previous
                // filter missed it and a slow first run stopped the host. The rethrow above keeps a
                // genuine shutdown distinguishable from a timeout.
                ReportUnavailable(exception.Message);
                return false;
            }
        }
    }
}
