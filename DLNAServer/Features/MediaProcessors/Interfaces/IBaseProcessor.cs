using DLNAServer.Database.Entities;
using DLNAServer.Helpers.Interfaces;

namespace DLNAServer.Features.MediaProcessors.Interfaces
{
    public interface IBaseProcessor : ITerminateAble, IInitializeAble
    {
        Task RefreshMetadataAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true);
        Task FillEmptyMetadataAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true);
        Task RefreshThumbnailsAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true);
        Task FillEmptyThumbnailsAsync(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true);
    }
}
