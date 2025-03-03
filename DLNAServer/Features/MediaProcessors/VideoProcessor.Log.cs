namespace DLNAServer.Features.MediaProcessors
{
    public partial class VideoProcessor
    {
        [LoggerMessage(1, LogLevel.Information, "Set metadata for file: '{file}'")]
        partial void InformationSetMetadata(string file);
        [LoggerMessage(2, LogLevel.Information, "Set thumbnail for file: '{file}'")]
        partial void InformationSetThumbnail(string file);
        [LoggerMessage(3, LogLevel.Debug, "Created thumbnail as '{file}' in {duration:0.00}(ms)")]
        partial void DebugCreateThumbnail(string file, double duration);
    }
}
