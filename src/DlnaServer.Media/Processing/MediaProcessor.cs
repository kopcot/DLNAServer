using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Processing;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Files;
using DlnaServer.Media.Processing.Metadata;
using DlnaServer.Media.Processing.Provisioning;
using DlnaServer.Media.Processing.Thumbnails;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Exceptions;

namespace DlnaServer.Media.Processing
{
    /// <inheritdoc cref="IMediaProcessor"/>
    internal sealed partial class MediaProcessor : IMediaProcessor
    {
        /// <summary>
        /// How long any one ffmpeg or ffprobe call may take before it is treated as hung.
        /// </summary>
        /// <remarks>
        /// Xabe offers no timeout of its own, and the only token these calls received was the hosted
        /// service's shutdown token - cancelled at shutdown and never before. So a truncated container,
        /// or a file on a drive that never spun up, blocked <c>ProcessOneAsync</c> indefinitely and the
        /// whole processing loop stopped advancing. <c>MaxFailureCount</c> could not retire the file
        /// either, because nothing threw. Generous on purpose: probing reads a container header and
        /// snapshotting decodes one frame, so anything past this is a hang rather than slow hardware.
        /// </remarks>
        private static readonly TimeSpan _ffmpegTimeout = TimeSpan.FromMinutes(3);

        private readonly IFFmpegProvisioner _ffmpeg;
        private readonly ImageThumbnailGenerator _imageThumbnails;
        private readonly ILogger<MediaProcessor> _logger;

        // The ffprobe call, a seam only so a test can make it time out: Xabe's entry point is static and
        // refuses to run without real binaries, so the timeout arm is otherwise unreachable in a test.
        private readonly Func<string, CancellationToken, Task<IMediaInfo>> _getMediaInfo;

        public MediaProcessor(
            IFFmpegProvisioner ffmpeg,
            ImageThumbnailGenerator imageThumbnails,
            ILogger<MediaProcessor> logger)
            : this(ffmpeg, imageThumbnails, logger, FFmpeg.GetMediaInfo)
        {
        }

        internal MediaProcessor(
            IFFmpegProvisioner ffmpeg,
            ImageThumbnailGenerator imageThumbnails,
            ILogger<MediaProcessor> logger,
            Func<string, CancellationToken, Task<IMediaInfo>> getMediaInfo)
        {
            _ffmpeg = ffmpeg;
            _imageThumbnails = imageThumbnails;
            _logger = logger;
            _getMediaInfo = getMediaInfo;
        }

        public async Task<MediaMetadataResult?> ExtractMetadataAsync(
            string filePath,
            DlnaMime mime,
            MediaProcessingSettings settings,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (mime.ToMedia() == DlnaMedia.Image)
            {
                // Images carry no stream metadata worth storing. Return an empty result rather than null
                // so the caller records the attempt and stops re-probing the file on every pass.
                // Tags are still read: a photograph carries the richest tag block of anything in a
                // library - camera, lens, exposure, date taken - and returning before this is why no
                // picture ever showed one. Through ImageTagReader rather than ffprobe, which cannot see
                // an EXIF block at all, and which would otherwise spawn a process per picture to learn
                // nothing. No ffmpeg is involved, so an image is still indexed with none present.
                return MediaMetadataResult.Empty with
                {
                    Tags = settings.ReadContainerTags
                        ? ImageTagReader.Read(filePath)
                        : [],
                };
            }

            if (IsUnsafeForFFmpeg(filePath))
            {
                LogUnsafePathRefused(filePath);
                return null;
            }

            if (!await _ffmpeg.EnsureAvailableAsync(settings.AllowFFmpegDownload, cancellationToken))
            {
                return null;
            }

            try
            {
                using var timeout = CreateTimeout(cancellationToken);

                var info = await _getMediaInfo(filePath, timeout.Token);

                return new MediaMetadataResult(
                    MapAudioStreams(info),
                    MapVideo(info),
                    MapSubtitles(info))
                {
                    Tags = await ReadContainerTagsAsync(filePath, settings, cancellationToken),
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The timeout fired, not the host stopping. Reported as a read failure so the caller
                // records the attempt - which is what lets MaxFailureCount retire the file.
                LogFFmpegTimedOut(filePath, (int)_ffmpegTimeout.TotalSeconds);
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // ffprobe is being pointed at arbitrary user files, so the failure surface is whatever
                // the container happens to be. Xabe alone throws ConversionException,
                // FFmpegNotFoundException, InvalidInputException, UnknownDecoderException and more, none
                // of which the previous ArgumentException/InvalidOperationException/IOException filter
                // matched - so a single bad container reached the hosted service and stopped the host.
                // "No metadata we could read" is the honest outcome for every one of them.
                LogMetadataFailed(filePath, FFmpegFailureReason.Summarise(exception.Message));
                return null;
            }
        }

        public async Task<GeneratedThumbnail?> GenerateThumbnailAsync(
            string filePath,
            DlnaMime mime,
            string targetPath,
            MediaProcessingSettings settings,
            bool allowAdoption = true,
            DateTime? notOlderThanUtc = null,
            TimeSpan? knownDuration = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var request = new ThumbnailRequest(
                settings.MaxWidth,
                settings.MaxHeight,
                settings.Quality,
                settings.ThumbnailMime,
                settings.StoreThumbnailContent);

            // Adopt a thumbnail that is already there rather than making it again. With thumbnails stored
            // beside the media, a library the reference has been previewing for years already has them -
            // and for video this is the difference between adopting a file and running ffmpeg over the
            // whole library. The reference makes the same check before doing any work.
            // Adoption is refused for an image older than the media, and refused outright when the caller
            // asked for a rebuild - otherwise "Recreate thumbnails" could never produce anything, because
            // it deletes only the database row and the file on disk was adopted straight back.
            if (allowAdoption
                && _imageThumbnails.Describe(targetPath, request, notOlderThanUtc) is { } existing)
            {
                return existing;
            }

            return mime.ToMedia() switch
            {
                DlnaMedia.Image => _imageThumbnails.Generate(filePath, targetPath, request),
                DlnaMedia.Video => await GenerateVideoThumbnailAsync(
                    filePath,
                    targetPath,
                    request,
                    settings,
                    knownDuration,
                    cancellationToken),
                DlnaMedia.Audio => await GenerateAudioThumbnailAsync(
                    filePath,
                    targetPath,
                    request,
                    settings,
                    cancellationToken),
                _ => null,
            };
        }

        /// <summary>
        /// Whether a path cannot be handed to ffmpeg or ffprobe without risking argument injection.
        /// </summary>
        /// <remarks>
        /// Xabe builds a single <c>ProcessStartInfo.Arguments</c> string and wraps paths in bare double
        /// quotes - it exposes no <c>ArgumentList</c>, so there is no way to pass an argument as one
        /// opaque token. .NET then re-parses that string using Windows quoting rules on Linux too, so a
        /// name containing a double quote closes the quoted region early and everything after it becomes
        /// further ffmpeg options. <c>-y &lt;path&gt;</c> alone is an arbitrary overwrite as the server
        /// user, and ffmpeg's protocol handlers reach the network. Anyone who can write into a source
        /// folder chooses these names, so they are untrusted input.
        /// <para>
        /// Refusing is deliberate rather than sanitising: the quoting cannot be done correctly through
        /// this dependency, and a name that needs escaping is vanishingly rare next to the consequence
        /// of getting it wrong. The file is reported as a processing failure, so it retires through
        /// <c>MaxFailureCount</c> instead of being retried forever. Carriage return and newline are
        /// refused for the same reason - they terminate an argument for some parsers.
        /// </para>
        /// </remarks>
        private static bool IsUnsafeForFFmpeg(string filePath)
        {
            return filePath.AsSpan().IndexOfAny('"', '\r', '\n') >= 0;
        }

        /// <summary>
        /// Grabs a single frame with ffmpeg and hands it to the image path for scaling and encoding.
        /// </summary>
        /// <remarks>
        /// The extracted frame goes to a temporary file that is always deleted, so a failure part-way
        /// through cannot leave stray frames accumulating in the cache directory.
        /// </remarks>
        private Task<GeneratedThumbnail?> GenerateVideoThumbnailAsync(
            string filePath,
            string targetPath,
            ThumbnailRequest request,
            MediaProcessingSettings settings,
            TimeSpan? knownDuration,
            CancellationToken cancellationToken)
        {
            return GenerateFrameThumbnailAsync(
                filePath,
                targetPath,
                request,
                settings,
                (info, framePath) =>
                {
                    var videoStream = info.VideoStreams.FirstOrDefault();

                    if (videoStream is null)
                    {
                        return null;
                    }

                    var captureAt = VideoCaptureTime.For(knownDuration ?? videoStream.Duration);

                    var conversion = FFmpeg.Conversions.New()
                        .AddStream(videoStream.SetOutputFramesCount(1).SetSeek(captureAt))
                        .SetOutput(framePath);

                    // A fast preset: this extracts one frame, and the reference used VerySlow here, which is
                    // an encoding-effort setting that buys nothing for a single still and costs real CPU.
                    _ = conversion.SetPreset(ConversionPreset.VeryFast);

                    return conversion;
                },
                cancellationToken);
        }

        /// <summary>
        /// Every tag the container carries about itself and its tracks, or nothing when it carries none.
        /// </summary>
        /// <remarks>
        /// A second ffprobe call, because the Xabe object model exposes only the handful of tags it has
        /// properties for - <c>Title</c> and <c>Language</c> - and there is no way to reach the rest
        /// through it. Asked of every kind of file: an image answers with little more than its format,
        /// which is honest, and gating it by kind would only hide the day a JPEG does carry something.
        /// <para>
        /// A failure here returns nothing rather than failing the whole extraction. The typed metadata
        /// the DLNA contract depends on has already been read by this point, and losing it over an
        /// unparseable tag block would trade something that matters for something that does not.
        /// </para>
        /// </remarks>
        private async Task<IReadOnlyList<MediaFileTagDto>> ReadContainerTagsAsync(
            string filePath,
            MediaProcessingSettings settings,
            CancellationToken cancellationToken)
        {
            if (!settings.ReadContainerTags)
            {
                return [];
            }

            // Guarded here rather than relying on the caller, because the image path reaches this without
            // having gone near ffmpeg. Resolving a positive answer is cached, so the audio and video paths
            // pay nothing for asking a second time.
            if (IsUnsafeForFFmpeg(filePath))
            {
                LogUnsafePathRefused(filePath);
                return [];
            }

            if (!await _ffmpeg.EnsureAvailableAsync(settings.AllowFFmpegDownload, cancellationToken))
            {
                return [];
            }

            try
            {
                // Its own timeout, because the image path reaches this outside the one the stream probe
                // runs under - and a hung ffprobe with nothing to cancel it is what stopped the whole
                // processing loop before any of these calls were given one.
                using var timeout = CreateTimeout(cancellationToken);

                // -v quiet keeps ffprobe's banner out of the JSON; the stream entries are asked for by
                // name so the reply carries the tag blocks and the index to attribute them to, and not a
                // full description of every stream.
                var json = await Probe.New().Start(
                    $"-v quiet -show_entries format_tags:stream=index:stream_tags -of json \"{filePath}\"",
                    timeout.Token);

                return ContainerTagReader.Parse(json);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                LogFFmpegTimedOut(filePath, (int)_ffmpegTimeout.TotalSeconds);
                return [];
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                LogContainerTagsFailed(filePath, FFmpegFailureReason.Summarise(exception.Message));
                return [];
            }
        }

        /// <summary>
        /// Writes the cover image embedded in an audio file and hands it to the image path.
        /// </summary>
        /// <remarks>
        /// Embedded artwork is carried as a single-frame video stream with the <c>attached_pic</c>
        /// disposition, so an MP3 with a cover reports one video stream and one without reports none -
        /// which is what tells the two apart here, and why no separate tag reader is needed.
        /// <para>
        /// A missing cover is not a failure: most of a library has none, and returning null lets the
        /// caller record it as "nothing to make" rather than retrying the file on every pass. Extracted
        /// to a temporary PNG rather than copied out, so a JPEG and a PNG cover take the same path.
        /// </para>
        /// </remarks>
        private Task<GeneratedThumbnail?> GenerateAudioThumbnailAsync(
            string filePath,
            string targetPath,
            ThumbnailRequest request,
            MediaProcessingSettings settings,
            CancellationToken cancellationToken)
        {
            return GenerateFrameThumbnailAsync(
                filePath,
                targetPath,
                request,
                settings,
                (info, framePath) =>
                {
                    var artwork = info.VideoStreams.FirstOrDefault();

                    if (artwork is null)
                    {
                        LogNoEmbeddedArtwork(filePath);
                        return null;
                    }

                    return FFmpeg.Conversions.New()
                        .AddStream(artwork)
                        .SetOutput(framePath);
                },
                cancellationToken);
        }

        // Shared by the video and audio thumbnails. buildConversion picks the stream and returns a
        // conversion writing to the frame path, or null when there is nothing to make one from - and must
        // not touch the disc, because the leftover frame is only deleted after it returns.
        private async Task<GeneratedThumbnail?> GenerateFrameThumbnailAsync(
            string filePath,
            string targetPath,
            ThumbnailRequest request,
            MediaProcessingSettings settings,
            Func<IMediaInfo, string, IConversion?> buildConversion,
            CancellationToken cancellationToken)
        {
            if (IsUnsafeForFFmpeg(filePath))
            {
                LogUnsafePathRefused(filePath);
                return null;
            }

            if (!await _ffmpeg.EnsureAvailableAsync(settings.AllowFFmpegDownload, cancellationToken))
            {
                return null;
            }

            var framePath = Path.Combine(
                Path.GetDirectoryName(targetPath)!,
                $"{Path.GetFileNameWithoutExtension(targetPath)}.frame.png");

            try
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                using var timeout = CreateTimeout(cancellationToken);

                // Probed under the timeout token, and the conversion built by hand rather than through
                // FromSnippet.Snapshot. That helper takes no token and calls GetMediaInfo itself, so the
                // probe ran outside the very timeout this method sets up - reopening the hang that
                // stopped the whole processing loop, and with MaxWorkers at 2 it takes only two such
                // files to stop it for good.
                //
                // It costs no extra work: Snapshot probed internally on every call anyway, so skipping
                // our own probe when knownDuration was supplied saved nothing - the fork and exec
                // happened regardless, just untimed and invisible. This is one probe either way.
                var info = await _getMediaInfo(filePath, timeout.Token);
                var conversion = buildConversion(info, framePath);

                if (conversion is null)
                {
                    return null;
                }

                // Deleted first rather than overwritten: a previous run killed part-way through leaves
                // the file behind, and ffmpeg then stops to ask whether to overwrite it, on stdin that
                // nothing is attached to - so the file stalled the full timeout on every later pass.
                TryDeleteFrame(framePath);

                await StartConversionAsync(conversion, filePath, framePath, timeout.Token);

                return _imageThumbnails.Generate(framePath, targetPath, request);
            }
            catch (ConversionException exception)
            {
                LogThumbnailConversionFailed(filePath, FFmpegFailureReason.Summarise(exception.Message));
                return null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The timeout fired, not the host stopping - see CreateTimeout.
                LogFFmpegTimedOut(filePath, (int)_ffmpegTimeout.TotalSeconds);
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                LogThumbnailFailed(filePath, FFmpegFailureReason.Summarise(exception.Message));
                return null;
            }
            finally
            {
                TryDeleteFrame(framePath);
            }
        }

        /// <summary>
        /// Runs a one-frame conversion, riding out the race in Xabe's output log.
        /// </summary>
        /// <remarks>
        /// Xabe.FFmpeg 6.0.2's <c>FFmpegWrapper.RunProcess</c> waits on the process handle and calls
        /// <c>WaitForExit()</c> - the overload that also drains standard error - only when the process has
        /// not exited yet. When it has, the stderr callback can still be appending to <c>_outputLog</c>
        /// while <c>ToArray</c> copies it, which throws "Destination array was not long enough". ffmpeg has
        /// already finished by then, so the frame is on disc and only the wrapper's bookkeeping failed.
        /// The frame is deleted before every conversion, so one that exists now was written by this one.
        /// </remarks>
        private async Task StartConversionAsync(
            IConversion conversion,
            string filePath,
            string framePath,
            CancellationToken cancellationToken)
        {
            try
            {
                _ = await conversion.Start(cancellationToken);
            }
            catch (ArgumentException exception) when (IsOutputLogRace(exception.ParamName, exception.StackTrace)
                && File.Exists(framePath))
            {
                LogOutputLogRaceRidden(filePath);
            }
        }

        /// <summary>
        /// Whether an exception is Xabe's output-log race rather than anything else that shares its type.
        /// </summary>
        /// <remarks>
        /// Taken apart into the two strings so both answers can be tested: the race cannot be provoked on
        /// demand, and any other <see cref="ArgumentException"/> must still count as a failure.
        /// </remarks>
        internal static bool IsOutputLogRace(string? paramName, string? stackTrace)
        {
            return string.Equals(paramName, "destinationArray", StringComparison.Ordinal)
                && stackTrace is not null
                && stackTrace.Contains("Xabe.FFmpeg.FFmpegWrapper", StringComparison.Ordinal);
        }

        /// <summary>
        /// A token that cancels when the host stops or when <see cref="_ffmpegTimeout"/> elapses.
        /// </summary>
        private static CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
        {
            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(_ffmpegTimeout);

            return linked;
        }

        /// <summary>
        /// Deletes the temporary frame without ever throwing.
        /// </summary>
        /// <remarks>
        /// This runs in a <c>finally</c>, so a throw here would replace whatever outcome the method had
        /// already reached - including masking the very exception the catch arms above just handled. A
        /// leftover frame is harmless by comparison: the next attempt overwrites it.
        /// </remarks>
        private void TryDeleteFrame(string framePath)
        {
            try
            {
                if (File.Exists(framePath))
                {
                    File.Delete(framePath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                LogFrameCleanupFailed(framePath, exception.Message);
            }
        }

        /// <remarks>
        /// Every track, not just the first: a film with several dubs carries one audio stream per language,
        /// and keeping only the first threw the rest away. Indexed the way
        /// <see cref="MapSubtitles"/> indexes its own - by position in the list rather than by the
        /// container's stream number, so the two are numbered consistently.
        /// </remarks>
        private static List<AudioStreamDto> MapAudioStreams(IMediaInfo info)
        {
            var streams = new List<AudioStreamDto>();
            var index = 0;

            foreach (var stream in info.AudioStreams)
            {
                streams.Add(new AudioStreamDto
                {
                    StreamIndex = index,
                    Title = stream.Title,
                    IsDefault = stream.Default == 1,
                    Duration = stream.Duration,
                    Codec = stream.Codec,
                    Bitrate = stream.Bitrate,
                    SampleRate = stream.SampleRate,
                    Channels = stream.Channels,
                    Language = stream.Language,
                });

                index++;
            }

            return streams;
        }

        private static VideoStreamDto? MapVideo(IMediaInfo info)
        {
            var stream = info.VideoStreams.FirstOrDefault();

            return stream is null
                ? null
                : new VideoStreamDto
                {
                    Duration = stream.Duration,
                    Width = stream.Width,
                    Height = stream.Height,
                    FrameRate = stream.Framerate,
                    AspectRatio = stream.Ratio,
                    Bitrate = stream.Bitrate,
                    PixelFormat = stream.PixelFormat,
                    Rotation = stream.Rotation,
                    Codec = stream.Codec,
                };
        }

        /// <summary>
        /// Every embedded subtitle track. The reference kept only the first.
        /// </summary>
        private static List<SubtitleStreamDto> MapSubtitles(IMediaInfo info)
        {
            var subtitles = new List<SubtitleStreamDto>();
            var index = 0;

            foreach (var stream in info.SubtitleStreams)
            {
                subtitles.Add(new SubtitleStreamDto
                {
                    StreamIndex = index,
                    Language = stream.Language,
                    Codec = stream.Codec,
                    ExternalFilePath = null,
                });

                index++;
            }

            return subtitles;
        }
    }
}
