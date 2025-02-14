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
        public async Task FillEmptyMetadata(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
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
                        await AudioProcessor.FillEmptyMetadata(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Image:
                        await ImageProcessor.FillEmptyMetadata(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Video:
                        await VideoProcessor.FillEmptyMetadata(group, setCheckedForFailed);
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported media type: {group.Key}");
                }
            }
        }
        public async Task FillEmptyThumbnails(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
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
                        await AudioProcessor.FillEmptyThumbnails(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Image:
                        await ImageProcessor.FillEmptyThumbnails(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Video:
                        await VideoProcessor.FillEmptyThumbnails(group, setCheckedForFailed);
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported media type: {group.Key}");
                }
            }
        }
        public async Task RefreshMetadata(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
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
                        await AudioProcessor.RefreshMetadata(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Image:
                        await ImageProcessor.RefreshMetadata(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Video:
                        await VideoProcessor.RefreshMetadata(group, setCheckedForFailed);
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported media type: {group.Key}");
                }
            }
        }
        public async Task RefreshThumbnails(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true)
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
                        await AudioProcessor.RefreshThumbnails(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Image:
                        await ImageProcessor.RefreshThumbnails(group, setCheckedForFailed);
                        break;
                    case DlnaMedia.Video:
                        await VideoProcessor.RefreshThumbnails(group, setCheckedForFailed);
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
