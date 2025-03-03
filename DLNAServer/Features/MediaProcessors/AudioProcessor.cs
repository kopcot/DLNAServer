using DLNAServer.Common;
using DLNAServer.Configuration;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.MediaProcessors.Interfaces;
using DLNAServer.Helpers.Files;
using DLNAServer.Helpers.Logger;
using DLNAServer.Types.DLNA;
using System.Threading.Channels;
using Xabe.FFmpeg;

namespace DLNAServer.Features.MediaProcessors
{
    public partial class AudioProcessor : IAudioProcessor, IDisposable
    {
        private readonly ILogger<AudioProcessor> _logger;
        private readonly ServerConfig _serverConfig;
        private readonly Lazy<IFileRepository> _fileRepositoryLazy;
        private IFileRepository FileRepository => _fileRepositoryLazy.Value;
        public AudioProcessor(
            ILogger<AudioProcessor> logger,
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
                    && fe.FileDlnaMime.ToDlnaMedia() == DlnaMedia.Audio
                    && !fe.IsMetadataChecked).ToArray();

            if (files.Length == 0)
            {
                return Task.CompletedTask;
            }

            return files.Length != 0 ? RefreshMetadataAsync(files, setCheckedForFailed) : Task.CompletedTask;
        }
        public async Task RefreshMetadataAsync(FileEntity[] fileEntities, bool setCheckedForFailed = true)
        {
            try
            {
                if (!_serverConfig.GenerateMetadataForLocalAudio
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
            var audioMetadata = await GetFileMetadataAsync(file);
            file.AudioMetadata = audioMetadata;
            if (audioMetadata != null ||
               (setCheckedForFailed && audioMetadata == null))
            {
                file.IsMetadataChecked = true;

                InformationSetMetadata(file.FilePhysicalFullPath);
            }
        }
        private async Task<MediaAudioEntity?> GetFileMetadataAsync(FileEntity fileEntity)
        {
            if (fileEntity == null)
            {
                return null;
            }

            FileInfo fileInfo = new(fileEntity.FilePhysicalFullPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                return null;
            }

            try
            {
                using (var cancellationTokenSource = new CancellationTokenSource(TimeSpanValues.Time5min))
                {
                    IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(fileEntity.FilePhysicalFullPath, cancellationTokenSource.Token);
                    fileEntity.FileSizeInBytes = mediaInfo.Size;
                    return (ExtractAudioMetadataAsync(ref mediaInfo));
                }
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
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
        public Task FillEmptyThumbnailsAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            var files = fileEntities
                .Where(static (fe) =>
                       fe != null
                    && fe.FileDlnaMime.ToDlnaMedia() == DlnaMedia.Audio
                    && !fe.IsThumbnailChecked).ToArray();

            return files.Length != 0 ? RefreshThumbnailsAsync(files, setCheckedForFailed) : Task.CompletedTask;
        }
        public async Task RefreshThumbnailsAsync(FileEntity[] fileEntities, bool setCheckedForFailed = true)
        {
            try
            {
                if (fileEntities.Length == 0)
                {
                    return;
                }

                if (fileEntities.Length == 1)
                {
                    var file = fileEntities.First();
                    file.IsThumbnailChecked = true;
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

                    var consumer = Task.Run(async () =>
                    {
                        await Parallel.ForEachAsync(
                            channel.Reader.ReadAllAsync(),
                            parallelOptions: new() { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                            async (file, cancellationToken) =>
                            {
                                file.IsThumbnailChecked = true;

                                await Task.CompletedTask;
                            });
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
        // ~AudioProcessor()
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
