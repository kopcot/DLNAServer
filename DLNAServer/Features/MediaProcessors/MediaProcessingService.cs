using DLNAServer.Database.Entities;
using DLNAServer.Features.MediaProcessors.Interfaces;
using DLNAServer.Types.DLNA;

namespace DLNAServer.Features.MediaProcessors
{
    public class MediaProcessingService : IMediaProcessingService
    {
        private readonly Lazy<IAudioProcessor> _audioProcessorLazy;
        private readonly Lazy<IImageProcessor> _imageProcessorLazy;
        private readonly Lazy<IVideoProcessor> _videoProcessorLazy;
        private IAudioProcessor AudioProcessor => _audioProcessorLazy.Value;
        private IImageProcessor ImageProcessor => _imageProcessorLazy.Value;
        private IVideoProcessor VideoProcessor => _videoProcessorLazy.Value;

        public MediaProcessingService(
            Lazy<IAudioProcessor> audioProcessorLazy,
            Lazy<IImageProcessor> imageProcessorLazy,
            Lazy<IVideoProcessor> videoProcessorLazy)
        {
            _audioProcessorLazy = audioProcessorLazy;
            _imageProcessorLazy = imageProcessorLazy;
            _videoProcessorLazy = videoProcessorLazy;
        }
        public async Task FillEmptyMetadataAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            if (!fileEntities.Any())
            {
                return;
            }

            var mediaGroup = fileEntities.GroupBy(fe => fe.FileDlnaMime.ToDlnaMedia()).ToArray();

            foreach (var group in mediaGroup)
            {
                switch (group.Key)
                {
                    case DlnaMedia.Audio:
                        await AudioProcessor.FillEmptyMetadataAsync(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Image:
                        await ImageProcessor.FillEmptyMetadataAsync(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Video:
                        await VideoProcessor.FillEmptyMetadataAsync(group, setCheckedForFailed);
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported media type: {group.Key}");
                }
            }
        }
        public async Task FillEmptyThumbnailsAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            if (!fileEntities.Any())
            {
                return;
            }

            var mediaGroup = fileEntities.GroupBy(fe => fe.FileDlnaMime.ToDlnaMedia()).ToArray();

            foreach (var group in mediaGroup)
            {
                switch (group.Key)
                {
                    case DlnaMedia.Audio:
                        await AudioProcessor.FillEmptyThumbnailsAsync(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Image:
                        await ImageProcessor.FillEmptyThumbnailsAsync(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Video:
                        await VideoProcessor.FillEmptyThumbnailsAsync(group, setCheckedForFailed);
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported media type: {group.Key}");
                }
            }
        }
        public async Task RefreshMetadataAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            if (!fileEntities.Any())
            {
                return;
            }

            var mediaGroup = fileEntities.GroupBy(fe => fe.FileDlnaMime.ToDlnaMedia()).ToArray();

            foreach (var group in mediaGroup)
            {
                switch (group.Key)
                {
                    case DlnaMedia.Audio:
                        await AudioProcessor.RefreshMetadataAsync(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Image:
                        await ImageProcessor.RefreshMetadataAsync(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Video:
                        await VideoProcessor.RefreshMetadataAsync(group, setCheckedForFailed);
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported media type: {group.Key}");
                }
            }
        }
        public async Task RefreshThumbnailsAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
        {
            if (!fileEntities.Any())
            {
                return;
            }

            var mediaGroup = fileEntities.GroupBy(fe => fe.FileDlnaMime.ToDlnaMedia()).ToArray();

            foreach (var group in mediaGroup)
            {
                switch (group.Key)
                {
                    case DlnaMedia.Audio:
                        await AudioProcessor.RefreshThumbnailsAsync(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Image:
                        await ImageProcessor.RefreshThumbnailsAsync(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Video:
                        await VideoProcessor.RefreshThumbnailsAsync(group, setCheckedForFailed);
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported media type: {group.Key}");
                }
            }
        }
        public Task InitializeAsync()
        {
            throw new NotImplementedException();
        }
        public Task TerminateAsync()
        {
            throw new NotImplementedException();
        }
    }
}
