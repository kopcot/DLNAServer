using DLNAServer.Configuration;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.MediaProcessors.Interfaces;
using DLNAServer.Helpers.Files;
using DLNAServer.Types.DLNA;
using System.Threading.Channels;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Exceptions;

namespace DLNAServer.Features.MediaProcessors
{
    public class VideoProcessor : IVideoProcessor, IDisposable
    {
        private readonly ILogger<VideoProcessor> _logger;
        private readonly ServerConfig _serverConfig;
        private readonly Lazy<IFileRepository> _fileRepositoryLazy;
        private IFileRepository FileRepository => _fileRepositoryLazy.Value;
        private static Snippets CreateThumbnailSnippets => FFmpeg.Conversions.FromSnippet;
        public VideoProcessor(
            ILogger<VideoProcessor> logger,
            ServerConfig serverConfig,
            Lazy<IFileRepository> fileRepositoryLazy)
        {
            _logger = logger;
            _serverConfig = serverConfig;
            _fileRepositoryLazy = fileRepositoryLazy;
        }
        public async Task InitializeAsync()
        {
            await FFmpegHelper.EnsureFFmpegDownloaded(_logger);
        }

        public async Task FillEmptyMetadataAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            var files = fileEntities
                .Where(static (fe) =>
                       fe != null
                    && fe.FileDlnaMime.ToDlnaMedia() == DlnaMedia.Video
                    && !fe.IsMetadataChecked).ToArray();

            if (files.Length == 0)
            {
                return;
            }

            await RefreshMetadataAsync(files, setCheckedForFailed);
        }
        public async Task RefreshMetadataAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            try
            {
                if (!_serverConfig.GenerateMetadataForLocalMovies
                    || !fileEntities.Any())
                {
                    return;
                }

                await FFmpegHelper.EnsureFFmpegDownloaded(_logger);

                if (fileEntities.Count() == 1)
                {
                    var file = fileEntities.First();
                    await RefreshSingleFileMetadataAsync(file, setCheckedForFailed);
                }
                else
                {
                    var fileEntitiesAsync = fileEntities.ToAsyncEnumerable();
                    var maxDegreeOfParallelism = Math.Min(fileEntities.Count(), (int)_serverConfig.ServerMaxDegreeOfParallelism);

                    var channel = Channel.CreateBounded<FileEntity>(new BoundedChannelOptions(maxDegreeOfParallelism)
                    {
                        FullMode = BoundedChannelFullMode.Wait, // Backpressure handling
                        SingleWriter = true,
                        SingleReader = false
                    });

                    var producer = Task.Run(async () =>
                    {
                        await foreach (var file in fileEntitiesAsync)
                        {
                            await channel.Writer.WriteAsync(file);
                        }
                        channel.Writer.Complete(); // Signal completion
                    });

                    var consumer = Parallel
                        .ForEachAsync(
                            channel.Reader.ReadAllAsync(),
                            parallelOptions: new() { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                            async (file, cancellationToken) =>
                            {
                                await RefreshSingleFileMetadataAsync(file, setCheckedForFailed);
                            });

                    await Task.WhenAll([producer, consumer]);
                }

                _ = await FileRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task RefreshSingleFileMetadataAsync(FileEntity file, bool setCheckedForFailed)
        {
            (var videoMetadata, var audioMetadata, var subtitleMetadata) = await GetVideoFileMetadataAsync(file);
            if (audioMetadata != null)
            {
                file.AudioMetadata = audioMetadata;
            }

            if (subtitleMetadata != null)
            {
                file.SubtitleMetadata = subtitleMetadata;
            }

            if (videoMetadata != null)
            {
                file.VideoMetadata = videoMetadata;
            }

            if (videoMetadata != null ||
               (setCheckedForFailed && videoMetadata == null))
            {
                file.IsMetadataChecked = true;

                _logger.LogInformation($"Set metadata for file: '{file.FilePhysicalFullPath}'");
            }
        }

        private async Task<(MediaVideoEntity?, MediaAudioEntity?, MediaSubtitleEntity?)> GetVideoFileMetadataAsync(FileEntity fileEntity)
        {
            if (fileEntity == null)
            {
                return (null, null, null);
            }

            FileInfo fileInfo = new(fileEntity.FilePhysicalFullPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                return (null, null, null);
            }

            try
            {
                using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                {
                    IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(fileEntity.FilePhysicalFullPath, cancellationTokenSource.Token);
                    var video = ExtractVideoMetadataAsync(ref mediaInfo);
                    var audio = ExtractAudioMetadataAsync(ref mediaInfo);
                    var subtitle = ExtractSubtitleMetadataAsync(ref mediaInfo);

                    fileEntity.FileSizeInBytes = mediaInfo.Size;
                    return (video, audio, subtitle);
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message, [fileEntity.FilePhysicalFullPath]);
                return (null, null, null);
            }
        }
        private MediaVideoEntity? ExtractVideoMetadataAsync(ref IMediaInfo mediaInfo)
        {
            try
            {
                if (mediaInfo.VideoStreams.FirstOrDefault() is IVideoStream videoStream)
                {
                    return new()
                    {
                        FilePhysicalFullPath = mediaInfo.Path,
                        Duration = videoStream.Duration,
                        Codec = videoStream.Codec,
                        Ratio = videoStream.Ratio,
                        Height = videoStream.Height,
                        Width = videoStream.Width,
                        Framerate = videoStream.Framerate,
                        Bitrate = videoStream.Bitrate,
                        PixelFormat = videoStream.PixelFormat,
                        Rotation = videoStream.Rotation,
                    };
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message, [mediaInfo.Path]);
                return null;
            }
        }
        private MediaAudioEntity? ExtractAudioMetadataAsync(ref IMediaInfo mediaInfo)
        {
            try
            {
                if (mediaInfo.AudioStreams.FirstOrDefault() is IAudioStream audioStream)
                {
                    return new()
                    {
                        FilePhysicalFullPath = mediaInfo.Path,
                        Duration = audioStream.Duration,
                        Codec = audioStream.Codec,
                        Bitrate = audioStream.Bitrate,
                        Channels = audioStream.Channels,
                        Language = audioStream.Language,
                        SampleRate = audioStream.SampleRate
                    };
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message, [mediaInfo.Path]);
                return null;
            }
        }
        private MediaSubtitleEntity? ExtractSubtitleMetadataAsync(ref IMediaInfo mediaInfo)
        {
            try
            {
                if (mediaInfo.SubtitleStreams.FirstOrDefault() is ISubtitleStream subtitleStream)
                {
                    return new()
                    {
                        FilePhysicalFullPath = mediaInfo.Path,
                        Language = subtitleStream.Language,
                        Codec = subtitleStream.Codec,
                    };
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message, [mediaInfo.Path]);
                return null;
            }
        }
        public async Task FillEmptyThumbnailsAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            var files = fileEntities
                .Where(static (fe) =>
                       fe != null
                    && fe.FileDlnaMime.ToDlnaMedia() == DlnaMedia.Video
                    && !fe.IsThumbnailChecked).ToArray();

            if (files.Length == 0)
            {
                return;
            }

            await RefreshThumbnailsAsync(files, setCheckedForFailed);
        }
        public async Task RefreshThumbnailsAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            try
            {
                if (!_serverConfig.GenerateThumbnailsForLocalMovies
                    || !fileEntities.Any())
                {
                    return;
                }

                await FFmpegHelper.EnsureFFmpegDownloaded(_logger);

                if (fileEntities.Count() == 1)
                {
                    var file = fileEntities.First();
                    await RefreshSingleFileThumbnailAsync(file, setCheckedForFailed);
                }
                else
                {
                    var fileEntitiesAsync = fileEntities.ToAsyncEnumerable();
                    var maxDegreeOfParallelism = Math.Min(fileEntities.Count(), (int)_serverConfig.ServerMaxDegreeOfParallelism);

                    var channel = Channel.CreateBounded<FileEntity>(new BoundedChannelOptions(maxDegreeOfParallelism)
                    {
                        FullMode = BoundedChannelFullMode.Wait, // Backpressure handling
                        SingleWriter = true,
                        SingleReader = false
                    });

                    var producer = Task.Run(async () =>
                    {
                        await foreach (var file in fileEntitiesAsync)
                        {
                            await channel.Writer.WriteAsync(file);
                        }
                        channel.Writer.Complete(); // Signal completion
                    });

                    var consumer = Parallel
                        .ForEachAsync(
                            channel.Reader.ReadAllAsync(),
                            parallelOptions: new() { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                            async (file, cancellationToken) =>
                            {
                                await RefreshSingleFileThumbnailAsync(file, setCheckedForFailed);
                            });

                    await Task.WhenAll([producer, consumer]);
                }

                _ = await FileRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task RefreshSingleFileThumbnailAsync(FileEntity file, bool setCheckedForFailed)
        {
            try
            {
                (var thumbnailFileFullPath, var thumbnailData, var dlnaMime, var dlnaProfileName) = await CreateThumbnailFromVideoAsync(
                    fileEntity: file,
                    dlnaMimeRequested: _serverConfig.DefaultDlnaMimeForVideoThumbnails)
                    ;

                if (thumbnailFileFullPath != null
                    && new FileInfo(thumbnailFileFullPath) is FileInfo thumbnailFileInfo
                    && thumbnailFileInfo.Exists)
                {
                    file.IsThumbnailChecked = true;
                    file.Thumbnail = new()
                    {
                        FilePhysicalFullPath = file.FilePhysicalFullPath,
                        ThumbnailFileDlnaMime = dlnaMime!.Value,
                        ThumbnailFileDlnaProfileName = dlnaProfileName,
                        ThumbnailFileExtension = thumbnailFileInfo.Extension,
                        ThumbnailFilePhysicalFullPath = thumbnailFileFullPath,
                        ThumbnailFileSizeInBytes = thumbnailFileInfo.Length,
                        ThumbnailData = _serverConfig.StoreThumbnailsForLocalMoviesInDatabase
                            ? new()
                            {
                                ThumbnailData = thumbnailData.ToArray(),
                                ThumbnailFilePhysicalFullPath = thumbnailFileFullPath,
                                FilePhysicalFullPath = file.FilePhysicalFullPath
                            }
                            : null
                    };

                    _logger.LogInformation($"Set thumbnail for file: '{file.FilePhysicalFullPath}'");
                }
                else if (setCheckedForFailed)
                {
                    file.IsThumbnailChecked = true;

                    _logger.LogInformation($"Set thumbnail for file: '{file.FilePhysicalFullPath}'");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message, [file.FilePhysicalFullPath, file.Thumbnail?.ThumbnailFilePhysicalFullPath]);
            }
        }

        private async Task<(string? thumbnailFileFullPath, ReadOnlyMemory<byte> thumbnailData, DlnaMime? dlnaMime, string? dlnaProfileName)> CreateThumbnailFromVideoAsync(FileEntity fileEntity, DlnaMime dlnaMimeRequested)
        {
            if (fileEntity == null)
            {
                return (null, ReadOnlyMemory<byte>.Empty, null, null);
            }

            FileInfo fileInfo = new(fileEntity.FilePhysicalFullPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                return (null, ReadOnlyMemory<byte>.Empty, null, null);
            }

            try
            {
                (var dlnaMime, var fileExtension, var dlnaProfileName) = ConvertDlnaMime(dlnaMimeRequested);

                string outputThumbnailFileFullPath = Path.Combine(fileEntity.Folder!, _serverConfig.SubFolderForThumbnail, fileEntity.FileName + fileExtension);
                {
                    FileInfo thumbnailFile = new(outputThumbnailFileFullPath);
                    var existsBefore = thumbnailFile.Exists;
                    if (!existsBefore)
                    {
                        FileHelper.CreateDirectoryIfNoExists(thumbnailFile.Directory);

                        using (var cancellationTokenSource_FileInfo = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                        {
                            IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(fileEntity.FilePhysicalFullPath, cancellationTokenSource_FileInfo.Token);
                            IVideoStream videoStream = mediaInfo.VideoStreams.FirstOrDefault()
                                    ?? throw new NullReferenceException(message: $"No video stream for file: {fileEntity.FilePhysicalFullPath}");

                            TimeSpan captureTime = GetCaptureTime(videoStream);
                            (var newHeight, var newWidth, var scaleFactor) = ThumbnailHelper.CalculateResize(videoStream.Height, videoStream.Width, (int)_serverConfig.MaxHeightForThumbnails, (int)_serverConfig.MaxWidthForThumbnails);

                            IConversion conversion = await CreateThumbnailSnippets.Snapshot(fileEntity.FilePhysicalFullPath, outputThumbnailFileFullPath, captureTime);
                            conversion = conversion.SetPreset(ConversionPreset.VerySlow)
                                .AddParameter("-err_detect ignore_err", ParameterPosition.PreInput)
                                .AddParameter("-loglevel panic", ParameterPosition.PreInput)
                                .AddParameter($"-vf scale={newWidth}:{newHeight}");

                            using (var cancellationTokenSource_Conversion = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                            {
                                var conversionResult = await conversion.Start(cancellationTokenSource_Conversion.Token);

                                _logger.LogDebug($"{DateTime.Now} Created thumbnail as {outputThumbnailFileFullPath}, StartTime = {conversionResult.StartTime}, EndTime = {conversionResult.EndTime}, Duration (ms) = {conversionResult.Duration.TotalMilliseconds}");
                            };
                        };
                    }
                };

                var thumbnailData = _serverConfig.StoreThumbnailsForLocalMoviesInDatabase
                    ? (await FileHelper.ReadFileAsync(outputThumbnailFileFullPath, _logger) ?? ReadOnlyMemory<byte>.Empty)
                    : ReadOnlyMemory<byte>.Empty;

                return (outputThumbnailFileFullPath, thumbnailData, dlnaMimeRequested, dlnaProfileName);
            }
            catch (ConversionException ex)
            {
                _logger.LogError(ex, ex.Message, [fileEntity.FilePhysicalFullPath]);
                await Task.Delay(1_000);
                return (null, ReadOnlyMemory<byte>.Empty, null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message, [fileEntity.FilePhysicalFullPath]);
                return (null, ReadOnlyMemory<byte>.Empty, null, null);
            }
        }
        private static TimeSpan GetCaptureTime(IVideoStream videoStream)
        {
            return videoStream.Duration > TimeSpan.FromHours(1) ? TimeSpan.FromMinutes(30)
                : videoStream.Duration > TimeSpan.FromMinutes(40) ? TimeSpan.FromMinutes(10)
                : videoStream.Duration > TimeSpan.FromMinutes(20) ? TimeSpan.FromMinutes(5)
                : videoStream.Duration > TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(1)
                : videoStream.Duration > TimeSpan.FromMinutes(2) ? TimeSpan.FromSeconds(30)
                : videoStream.Duration > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(10)
                : videoStream.Duration > TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(2)
                : TimeSpan.FromSeconds(0);
        }

        private (DlnaMime dlnaMime, string fileExtension, string dlnaProfileName) ConvertDlnaMime(DlnaMime dlnaMimeRequested)
        {
            DlnaMime dlnaMime;
            string fileExtension;
            string dlnaProfileName;

            var fileExtensions = _serverConfig.MediaFileExtensions.Where(e => e.Value.Key == dlnaMimeRequested).ToArray();
            if (fileExtensions.Length > 0)
            {
                dlnaMime = ConvertFromDlnaMime(fileExtensions.First().Value.Key);
                fileExtension = fileExtensions.First().Key;
                dlnaProfileName = fileExtensions.First().Value.Value
                    ?? fileExtensions.First().Value.Key.ToMainProfileNameString()
                    ?? throw new ArgumentNullException($"No defined DLNA Profile Name for {fileExtensions.First().Value.Key}");
            }
            else
            {
                dlnaMime = ConvertFromDlnaMime(dlnaMimeRequested);
                fileExtension = dlnaMimeRequested.DefaultFileExtensions().First();
                dlnaProfileName = dlnaMimeRequested.ToMainProfileNameString()
                    ?? throw new ArgumentNullException($"No defined DLNA Profile Name for {dlnaMimeRequested}");
            }

            return (dlnaMime, fileExtension, dlnaProfileName);
        }
        private static DlnaMime ConvertFromDlnaMime(DlnaMime dlnaMime)
        {
            return dlnaMime switch
            {
                DlnaMime.ImageBmp => DlnaMime.ImageBmp,
                DlnaMime.ImageGif => DlnaMime.ImageGif,
                DlnaMime.ImageJpeg => DlnaMime.ImageJpeg,
                DlnaMime.ImagePng => DlnaMime.ImagePng,
                DlnaMime.ImageWebp => DlnaMime.ImageWebp,
                DlnaMime.ImageXIcon => DlnaMime.ImageXIcon,
                DlnaMime.ImageXWindowsBmp => DlnaMime.ImageXWindowsBmp,
                _ => throw new NotImplementedException($"Not defined image format = {dlnaMime}"),
            };
        }

        public async Task TerminateAsync()
        {
            await Task.CompletedTask;
        }
        #region Dispose
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~VideoProcessor()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
