namespace DLNAServer.Controllers
{
    public partial class SCPDController
    {
        [LoggerMessage(1, LogLevel.Warning, "File not exists: {file}")]
        partial void WarningFileNotExists(string file);
    }
}
