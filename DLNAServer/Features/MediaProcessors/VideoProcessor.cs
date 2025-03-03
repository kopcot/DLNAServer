using DLNAServer.Common;
using DLNAServer.Configuration;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.MediaProcessors.Interfaces;
using DLNAServer.Helpers.Files;
using DLNAServer.Helpers.Logger;
using DLNAServer.Types.DLNA;
using System.Buffers;
using System.Threading.Channels;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Exceptions;

namespace DLNAServer.Features.MediaProcessors
{
    public partial class VideoProcessor : IVideoProcessor, IDisposable
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
        public Task InitializeAsync()
        {
            return FFmpegHelper.EnsureFFmpegDownloaded(_logger);
        }

        public Task FillEmptyMetadataAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            var files = fileEntities
                .Where(static (fe) =>
                       fe != null
                    && fe.FileDlnaMime.ToDlnaMedia() == DlnaMedia.Video
                    && !fe.IsMetadataChecked).ToArray();

            return files.Length != 0 ? RefreshMetadataAsync(files, setCheckedForFailed) : Task.CompletedTask;
        }
        public async Task RefreshMetadataAsync(FileEntity[] fileEntities, bool setCheckedForFailed = true)
        {
            try
            {
                if (!_serverConfig.GenerateMetadataForLocalMovies
                    || fileEntities.Length == 0)
                {
                    return;
                }

                await FFmpegHelper.EnsureFFmpegDownloaded(_logger);

                if (fileEntities.Length == 1)
                {
                    var file = fileEntities.First();
                    await RefreshSingleFileMetadataAsync(file, setCheckedForFailed);
                }
                else
                {
                    var maxDegreeOfParallelism = Math.Min(fileEntities.Length, (int)_serverConfig.ServerMaxDegreeOfParallelism);

                    var channel = Channel.CreateBounded<FileEntity>(new BoundedChannelOptions(maxDegreeOfParallelism)
                    {
                        FullMode = BoundedChannelFullMode.Wait, // Backpressure handling
                        SingleWriter = true,
                        SingleReader = false
                    });

                    var producer = Task.Run(async () =>
                    {
                        foreach (var file in fileEntities)
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
                _logger.LogGeneralErrorMessage(ex);
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

                InformationSetMetadata(file.FilePhysicalFullPath);
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
                using (var cancellationTokenSource = new CancellationTokenSource(TimeSpanValues.Time5min))
                {
                    IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(fileEntity.FilePhysicalFullPath, cancellationTokenSource.Token);
                    fileEntity.FileSizeInBytes = mediaInfo.Size;
                    return (ExtractVideoMetadataAsync(ref mediaInfo),
                            ExtractAudioMetadataAsync(ref mediaInfo),
                            ExtractSubtitleMetadataAsync(ref mediaInfo));
                }
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
                return (null, null, null);
            }
        }
        private MediaVideoEntity? ExtractVideoMetadataAsync(ref readonly IMediaInfo mediaInfo)
        {
            try
            {
                if (mediaInfo.VideoStreams.FirstOrDefault() is IVideoStream videoStream)
                {
                    return new()
                    {
                        FilePhysicalFullPath = mediaInfo.Path,
                        Duration = videoStream.Duration,
                        Codec = !string.IsNullOrWhiteSpace(videoStream.Codec)
                            ? string.Intern(videoStream.Codec)
                            : null,
                        Ratio = !string.IsNullOrWhiteSpace(videoStream.Ratio)
                            ? string.Intern(videoStream.Ratio)
                            : null,
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
                _logger.LogGeneralErrorMessage(ex);
                return null;
            }
        }
        private MediaAudioEntity? ExtractAudioMetadataAsync(ref readonly IMediaInfo mediaInfo)
        {
            try
            {
                if (mediaInfo.AudioStreams.FirstOrDefault() is IAudioStream audioStream)
                {
                    return new()
                    {
                        FilePhysicalFullPath = mediaInfo.Path,
                        Duration = audioStream.Duration,
                        Codec = !string.IsNullOrWhiteSpace(audioStream.Codec)
                            ? string.Intern(audioStream.Codec)
                            : null,
                        Bitrate = audioStream.Bitrate,
                        Channels = audioStream.Channels,
                        Language = !string.IsNullOrWhiteSpace(audioStream.Language)
                            ? string.Intern(audioStream.Language)
                            : null,
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
                _logger.LogGeneralErrorMessage(ex);
                return null;
            }
        }
        private MediaSubtitleEntity? ExtractSubtitleMetadataAsync(ref readonly IMediaInfo mediaInfo)
        {
            try
            {
                if (mediaInfo.SubtitleStreams.FirstOrDefault() is ISubtitleStream subtitleStream)
                {
                    return new()
                    {
                        FilePhysicalFullPath = mediaInfo.Path,
                        Language = !string.IsNullOrWhiteSpace(subtitleStream.Language)
                            ? string.Intern(subtitleStream.Language)
                            : null,
                        Codec = !string.IsNullOrWhiteSpace(subtitleStream.Codec)
                            ? string.Intern(subtitleStream.Codec)
                            : null,
                    };
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
                return null;
            }
        }
        public Task FillEmptyThumbnailsAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            var files = fileEntities
                .Where(static (fe) =>
                       fe != null
                    && fe.FileDlnaMime.ToDlnaMedia() == DlnaMedia.Video
                    && !fe.IsThumbnailChecked).ToArray();

            return files.Length != 0 ? RefreshThumbnailsAsync(files, setCheckedForFailed) : Task.CompletedTask;
        }
        public async Task RefreshThumbnailsAsync(FileEntity[] fileEntities, bool setCheckedForFailed = true)
        {
            try
            {
                if (!_serverConfig.GenerateThumbnailsForLocalMovies
                    || fileEntities.Length == 0)
                {
                    return;
                }

                await FFmpegHelper.EnsureFFmpegDownloaded(_logger);

                if (fileEntities.Length == 1)
                {
                    var file = fileEntities.First();
                    await RefreshSingleFileThumbnailAsync(file, setCheckedForFailed);
                }
                else
                {
                    var maxDegreeOfParallelism = Math.Min(fileEntities.Length, (int)_serverConfig.ServerMaxDegreeOfParallelism);

                    var channel = Channel.CreateBounded<FileEntity>(new BoundedChannelOptions(maxDegreeOfParallelism)
                    {
                        FullMode = BoundedChannelFullMode.Wait, // Backpressure handling
                        SingleWriter = true,
                        SingleReader = false
                    });

                    var producer = Task.Run(async () =>
                    {
                        foreach (var file in fileEntities)
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
                _logger.LogGeneralErrorMessage(ex);
            }
        }

        private async Task RefreshSingleFileThumbnailAsync(FileEntity file, bool setCheckedForFailed)
        {
            try
            {
                (var thumbnailFileFullPath, var thumbnailData, var dlnaMime, var dlnaProfileName) = await CreateThumbnailFromVideoAsync(
                    fileEntity: file,
                    dlnaMimeRequested: _serverConfig.DefaultDlnaMimeForVideoThumbnails);

                if (thumbnailFileFullPath != null
                    && new FileInfo(thumbnailFileFullPath) is FileInfo thumbnailFileInfo
                    && thumbnailFileInfo.Exists)
                {
                    file.IsThumbnailChecked = true;
                    file.Thumbnail = new()
                    {
                        FilePhysicalFullPath = file.FilePhysicalFullPath,
                        ThumbnailFileDlnaMime = dlnaMime!.Value,
                        ThumbnailFileDlnaProfileName = dlnaProfileName != null ? string.Intern(dlnaProfileName) : null,
                        ThumbnailFileExtension = string.Intern(thumbnailFileInfo.Extension),
                        ThumbnailFilePhysicalFullPath = thumbnailFileFullPath,
                        ThumbnailFileSizeInBytes = thumbnailFileInfo.Length,
                        ThumbnailData = _serverConfig.StoreThumbnailsForLocalMoviesInDatabase
                            ? new()
                            {
                                ThumbnailData = thumbnailData.ToArray(),
                                ThumbnailFilePhysicalFullPath = thumbnailFileFullPath,
                                FilePhysicalFullPath = file.FilePhysicalFullPath,
                            }
                            : null
                    };

                    InformationSetThumbnail(file.FilePhysicalFullPath);
                }
                else if (setCheckedForFailed)
                {
                    file.IsThumbnailChecked = true;

                    InformationSetThumbnail(file.FilePhysicalFullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
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
                FileInfo thumbnailFile = new(outputThumbnailFileFullPath);
                var existsBefore = thumbnailFile.Exists;
                if (!existsBefore)
                {
                    FileHelper.CreateDirectoryIfNoExists(thumbnailFile.Directory);

                    using (var cancellationTokenSource_FileInfo = new CancellationTokenSource(TimeSpanValues.Time5min))
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
                            .AddParameter(string.Format("-vf scale={0}:{1}", [newWidth, newHeight]));

                        using (var cancellationTokenSource_Conversion = new CancellationTokenSource(TimeSpanValues.Time5min))
                        {
                            var conversionResult = await conversion.Start(cancellationTokenSource_Conversion.Token);

                            DebugCreateThumbnail(outputThumbnailFileFullPath, conversionResult.Duration.TotalMilliseconds);
                        }
                    }
                }

                var thumbnailData = _serverConfig.StoreThumbnailsForLocalMoviesInDatabase
                    ? (await FileHelper.ReadFileAsync(outputThumbnailFileFullPath, _logger) ?? ReadOnlyMemory<byte>.Empty)
                    : ReadOnlyMemory<byte>.Empty;

                return (outputThumbnailFileFullPath, thumbnailData, dlnaMimeRequested, dlnaProfileName);
            }
            catch (ConversionException ex)
            {
                _logger.LogGeneralErrorMessage(ex);
                await Task.Delay(1_000);
                return (null, ReadOnlyMemory<byte>.Empty, null, null);
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
                return (null, ReadOnlyMemory<byte>.Empty, null, null);
            }
        }
        private ReadOnlyMemory<byte> method1()
        {
            //...
            byte[] bytes = new byte[10000];

            return bytes;
        }
        private ReadOnlyMemory<byte> method2()
        {
            //...
            byte[] bytes = new byte[10000];

            return new ReadOnlyMemory<byte>(bytes);
        }
        private static TimeSpan GetCaptureTime(IVideoStream videoStream)
        {
            return videoStream.Duration > TimeSpanValues.Time1hour ? TimeSpanValues.Time30min
                : videoStream.Duration > TimeSpanValues.Time40min ? TimeSpanValues.Time10min
                : videoStream.Duration > TimeSpanValues.Time20min ? TimeSpanValues.Time5min
                : videoStream.Duration > TimeSpanValues.Time5min ? TimeSpanValues.Time1min
                : videoStream.Duration > TimeSpanValues.Time2min ? TimeSpanValues.Time30sec
                : videoStream.Duration > TimeSpanValues.Time30sec ? TimeSpanValues.Time10sec
                : videoStream.Duration > TimeSpanValues.Time5sec ? TimeSpanValues.Time2sec
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

        public Task TerminateAsync()
        {
            return Task.CompletedTask;
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
