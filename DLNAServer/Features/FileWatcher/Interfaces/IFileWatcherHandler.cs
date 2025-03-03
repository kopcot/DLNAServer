using DLNAServer.Helpers.Interfaces;

namespace DLNAServer.Features.FileWatcher.Interfaces
{
    public interface IFileWatcherHandler : ITerminateAble
    {
        void WatchPath(string pathToWatch);
        void EnableRaisingEvents(bool enable);
    }
}
