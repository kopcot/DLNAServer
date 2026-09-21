namespace DlnaServer.Host.Controllers
{
    public sealed partial class StaticResourceController
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "Static resource '{FileName}' was requested but does not exist")]
        private partial void LogResourceNotFound(string fileName);
    }
}
