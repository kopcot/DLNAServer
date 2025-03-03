using DLNAServer.Database.Entities;
using DLNAServer.Helpers.Interfaces;
using DLNAServer.Types.DLNA;

namespace DLNAServer.Features.MediaContent.Interfaces
{
    public interface IContentExplorerManager : IInitializeAble, ITerminateAble
    {
        Task RefreshFoundFilesAsync(Dictionary<DlnaMime, IEnumerable<string>> inputFiles, bool shouldBeAdded);
        Task<List<DirectoryEntity>> GetNewDirectoryEntities(IEnumerable<string?> folders);
        Task<(FileEntity[] fileEntities, DirectoryEntity[] directoryEntities, bool isRootFolder, uint totalMatches)> GetBrowseResultItems(
            string objectID,
            int startingIndex,
            int requestedCount
            );
        Task<IEnumerable<FileEntity>> CheckFilesExistingAsync(IEnumerable<FileEntity> fileEntities);
        Task<IEnumerable<DirectoryEntity>> CheckDirectoriesExistingAsync(IEnumerable<DirectoryEntity> directoryEntities);
        Task ClearThumbnailsAsync(IEnumerable<FileEntity> files, bool deleteThumbnailFile = true);
        Task ClearAllThumbnailsAsync(bool deleteThumbnailFile = true);
        Task ClearAllMetadataAsync();
        Task ClearMetadataAsync(IEnumerable<FileEntity> files);
    }
}
