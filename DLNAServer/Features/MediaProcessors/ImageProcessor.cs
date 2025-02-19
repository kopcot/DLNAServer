﻿using DLNAServer.Configuration;
using DLNAServer.Database.Entities;
using DLNAServer.Database.Repositories.Interfaces;
using DLNAServer.Features.MediaProcessors.Interfaces;
using DLNAServer.Helpers.Files;
using DLNAServer.Types.DLNA;
using SkiaSharp;
using System.Buffers;

namespace DLNAServer.Features.MediaProcessors
{
    public class ImageProcessor : IImageProcessor, IDisposable
    {
        private readonly ILogger<ImageProcessor> _logger;
        private readonly ServerConfig _serverConfig;
        private readonly Lazy<IFileRepository> _fileRepositoryLazy;
        private IFileRepository FileRepository => _fileRepositoryLazy.Value;
        public ImageProcessor(
            ILogger<ImageProcessor> logger,
            ServerConfig serverConfig,
            Lazy<IFileRepository> fileRepositoryLazy)
        {
            _logger = logger;
            _serverConfig = serverConfig;
            _fileRepositoryLazy = fileRepositoryLazy;
        }
        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }
        public async Task FillEmptyMetadata(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            var files = fileEntities
                .Where(static (fe) =>
                       fe != null
                    && fe.FileDlnaMime.ToDlnaMedia() == DlnaMedia.Image
                    && !fe.IsMetadataChecked).ToArray();

            if (files.Length == 0)
            {
                return;
            }

            await RefreshMetadata(files, setCheckedForFailed);
        }
        public async Task RefreshMetadata(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            try
            {
                if (!_serverConfig.GenerateMetadataForLocalImages
                    || !fileEntities.Any())
                {
                    return;
                }

                await Parallel.ForEachAsync(
                    fileEntities,
                    parallelOptions: new() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    async (file, cancellationToken) =>
                    {
                        file.IsMetadataChecked = true;

                        await Task.CompletedTask;
                    });

                _ = await FileRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task FillEmptyThumbnails(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            var files = fileEntities
                .Where(static (fe) =>
                       fe != null
                    && fe.FileDlnaMime.ToDlnaMedia() == DlnaMedia.Image
                    && !fe.IsThumbnailChecked).ToArray();

            if (files.Length == 0)
            {
                return;
            }

            await RefreshThumbnails(files, setCheckedForFailed);
        }
        public async Task RefreshThumbnails(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            try
            {
                if (!_serverConfig.GenerateThumbnailsForLocalImages
                    || !fileEntities.Any())
                {
                    return;
                }

                await Parallel.ForEachAsync(
                    fileEntities,
                    parallelOptions: new() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    async (file, cancellationToken) =>
                    {
                        try
                        {
                            (var thumbnailFileFullPath, var thumbnailData, var dlnaMime, var dlnaProfileName) = await CreateThumbnailFromImage(file, _serverConfig.DefaultDlnaMimeForImageThumbnails);

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
                                    ThumbnailData = _serverConfig.StoreThumbnailsForLocalImagesInDatabase
                                        ? new()
                                        {
                                            ThumbnailData = thumbnailData,
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
                    });

                _ = await FileRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }
        private async Task<(string? thumbnailFileFullPath, byte[] thumbnailData, DlnaMime? dlnaMime, string? dlnaProfileName)> CreateThumbnailFromImage(FileEntity fileEntity, DlnaMime dlnaMimeRequested)
        {
            if (fileEntity == null)
            {
                return (null, [], null, null);
            }

            FileInfo fileInfo = new(fileEntity.FilePhysicalFullPath);
            if (!fileInfo.Exists)
            {
                return (null, [], null, null);
            }

            try
            {
                // can be done easier, but this way is using lower memory on the Linux → QNAP NAS 
                //
                // needed to have global env.variable on Linux set "MALLOC_TRIM_THRESHOLD_"
                // otherwise, memory will grow up with each Thumbnail creation with usage of SkiaSharp
                SKEncodedImageFormat imageFormat;
                string fileExtension;
                string dlnaProfileName;
                int newHeight;
                int newWidth;
                double scaleFactor;

                await using (var fileStream = new FileStream(fileEntity.FilePhysicalFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var data = SKData.Create(fileStream))
                {
                    using (var codec = SKCodec.Create(data))
                    {
                        if (codec == null)
                        {
                            throw new NullReferenceException($"Unable to create SKCodec for {fileEntity.FilePhysicalFullPath}");
                        }

                        (imageFormat, fileExtension, dlnaProfileName) = ConvertDlnaMime(dlnaMimeRequested);
                        (newHeight, newWidth, scaleFactor) = ThumbnailHelper.CalculateResize(codec.Info.Height, codec.Info.Width, (int)_serverConfig.MaxHeightForThumbnails, (int)_serverConfig.MaxWidthForThumbnails);
                    };

                    var outputThumbnailFileFullPath = Path.Combine(fileEntity.Folder!, _serverConfig.SubFolderForThumbnail, fileEntity.FileName + fileExtension);
                    FileInfo thumbnailFile = new(outputThumbnailFileFullPath);
                    var existsBefore = thumbnailFile.Exists;
                    if (!existsBefore)
                    {
                        FileHelper.CreateDirectoryIfNoExists(thumbnailFile.Directory);
                        {
                            using (var sourceBitmap = SKBitmap.Decode(data.Span))
                            using (var resizedImage = sourceBitmap.Resize(new SKImageInfo(newWidth, newHeight), samplingOptions))
                            using (var encodedImage = resizedImage.Encode(imageFormat, (int)_serverConfig.QualityForThumbnails))
                            {
                                var thumbnailData = encodedImage.ToArray();

                                await using (var fileStreamThumbnailFile = new FileStream(outputThumbnailFileFullPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                                {
                                    fileStreamThumbnailFile.Write(thumbnailData.AsSpan());
                                }
                                _logger.LogDebug($"{DateTime.Now} Created thumbnail as {outputThumbnailFileFullPath}");

                                return (outputThumbnailFileFullPath, thumbnailData, ConvertToDlnaMime(imageFormat), dlnaProfileName);
                            };
                        };
                    }
                    else
                    {
                        var thumbnailData = _serverConfig.StoreThumbnailsForLocalMoviesInDatabase
                            ? (await FileHelper.ReadFileAsync(outputThumbnailFileFullPath, _logger) ?? [])
                            : [];

                        return (outputThumbnailFileFullPath, thumbnailData, ConvertToDlnaMime(imageFormat), dlnaProfileName);
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{ex.Message} {fileEntity.FilePhysicalFullPath}", [fileEntity.FilePhysicalFullPath]);
                return (null, [], null, null);
            }
        }
        private static readonly SKSamplingOptions samplingOptions = new(SKFilterMode.Nearest, SKMipmapMode.Nearest);
        private (SKEncodedImageFormat dlnaMime, string fileExtension, string dlnaProfileName) ConvertDlnaMime(DlnaMime dlnaMimeRequested)
        {
            SKEncodedImageFormat imageFormat;
            string fileExtension;
            string dlnaProfileName;

            var fileExtensions = _serverConfig.MediaFileExtensions.Where(e => e.Value.Key == dlnaMimeRequested).ToArray();
            if (fileExtensions.Length > 0)
            {
                imageFormat = ConvertFromDlnaMime(fileExtensions.First().Value.Key);
                fileExtension = fileExtensions.First().Key;
                dlnaProfileName = fileExtensions.First().Value.Value
                    ?? fileExtensions.First().Value.Key.ToMainProfileNameString()
                    ?? throw new NullReferenceException($"No defined DLNA Profile Name for {fileExtensions.First().Value.Key}");
            }
            else
            {
                imageFormat = ConvertFromDlnaMime(dlnaMimeRequested);
                fileExtension = dlnaMimeRequested.DefaultFileExtensions().First();
                dlnaProfileName = dlnaMimeRequested.ToMainProfileNameString()
                    ?? throw new NullReferenceException($"No defined DLNA Profile Name for {dlnaMimeRequested}");
            }

            return (imageFormat, fileExtension, dlnaProfileName);
        }
        private static SKEncodedImageFormat ConvertFromDlnaMime(DlnaMime dlnaMime)
        {
            return dlnaMime switch
            {
                DlnaMime.ImageBmp => SKEncodedImageFormat.Bmp,
                DlnaMime.ImageGif => SKEncodedImageFormat.Gif,
                DlnaMime.ImageJpeg => SKEncodedImageFormat.Jpeg,
                DlnaMime.ImagePng => SKEncodedImageFormat.Png,
                DlnaMime.ImageWebp => SKEncodedImageFormat.Webp,
                DlnaMime.ImageXIcon => SKEncodedImageFormat.Ico,
                DlnaMime.ImageXWindowsBmp => SKEncodedImageFormat.Wbmp,
                _ => throw new NotImplementedException($"Not defined Mime type = {dlnaMime}"),
            };
        }
        private static DlnaMime ConvertToDlnaMime(SKEncodedImageFormat encodedImageFormat)
        {
            return encodedImageFormat switch
            {
                SKEncodedImageFormat.Bmp => DlnaMime.ImageBmp,
                SKEncodedImageFormat.Gif => DlnaMime.ImageGif,
                SKEncodedImageFormat.Jpeg => DlnaMime.ImageJpeg,
                SKEncodedImageFormat.Png => DlnaMime.ImagePng,
                SKEncodedImageFormat.Webp => DlnaMime.ImageWebp,
                SKEncodedImageFormat.Ico => DlnaMime.ImageXIcon,
                SKEncodedImageFormat.Wbmp => DlnaMime.ImageXWindowsBmp,
                _ => throw new NotImplementedException($"Not defined image format = {encodedImageFormat}"),
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
        // ~ImageProcessor()
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
