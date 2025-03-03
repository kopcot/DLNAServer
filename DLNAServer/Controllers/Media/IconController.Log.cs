namespace DLNAServer.Controllers.Media
{
    public partial class IconController
    {
        [LoggerMessage(1, LogLevel.Warning, "File not found, {filePath}")]
        partial void WarningFileNotFound(string filePath);
    }
}
