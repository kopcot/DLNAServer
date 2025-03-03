namespace DLNAServer.Features.MediaProcessors
{
    public partial class AudioProcessor
    {
        [LoggerMessage(1, LogLevel.Information, "Set metadata for file: '{file}'")]
        partial void InformationSetMetadata(string file);
        [LoggerMessage(2, LogLevel.Information, "Set thumbnail for file: '{file}'")]
        partial void InformationSetThumbnail(string file);
    }
}
