using DLNAServer.Helpers.Interfaces;

namespace DLNAServer.Features.FileWatcher.Interfaces
{
    public interface IFileWatcherManager : IInitializeAble, ITerminateAble
    {
        Task HandleFileCreatedChanged(string fileFullPath, WatcherChangeTypes eventAction, DateTime eventTimestamp);
        Task HandleFileRenamed(string newFileFullPath, string oldFileFullPath, WatcherChangeTypes eventAction, DateTime eventTimestamp);
        Task HandleFileRemove(string fileFullPath, WatcherChangeTypes eventAction, DateTime eventTimestamp);
        Task HandleDirectoryRemove(string fileFullPath, WatcherChangeTypes eventAction, DateTime eventTimestamp);
        Task HandleDirectoryRenamed(string newDirectoryFullPath, string oldDirectoryFullPath, WatcherChangeTypes eventAction, DateTime eventTimestamp);
        ulong UpdatesCount { get; }
    }
}
