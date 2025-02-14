using DLNAServer.Database.Entities;
using DLNAServer.Helpers.Interfaces;

namespace DLNAServer.Features.MediaProcessors.Interfaces
{
    public interface IBaseProcessor : ITerminateAble, IInitializeAble
    {
        Task RefreshMetadata(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true);
        Task FillEmptyMetadata(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true);
        Task RefreshThumbnails(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true);
        Task FillEmptyThumbnails(IEnumerable<FileEntity> fileEntities, bool setCheckedForFailed = true);
    }
}
