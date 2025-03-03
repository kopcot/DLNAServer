namespace DLNAServer.Controllers
{
    public partial class EventController
    {
        [LoggerMessage(1, LogLevel.Warning, "Incorrect subscribe request parameters. Callback = '{callback}', Timeout = '{timeout}', TimeoutNumber = {timeoutNumber}")]
        partial void WarningIncorrectRequestParameter(string? callback, string? timeout, int timeoutNumber);
    }
}
