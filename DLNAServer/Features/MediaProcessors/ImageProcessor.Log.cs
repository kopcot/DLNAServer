namespace DLNAServer.Features.MediaProcessors
{
    public partial class ImageProcessor
    {
        [LoggerMessage(1, LogLevel.Information, "Set thumbnail for file: '{file}'")]
        partial void InformationSetThumbnail(string file);
        [LoggerMessage(2, LogLevel.Debug, "Created thumbnail as '{file}'")]
        partial void DebugCreateThumbnail(string file);
    }
}
