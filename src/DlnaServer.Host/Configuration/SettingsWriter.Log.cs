namespace DlnaServer.Host.Configuration
{
    internal sealed partial class SettingsWriter
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Settings saved to '{ConfigurationPath}'; the configuration provider reloads it")]
        private partial void LogSaved(string configurationPath);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "'{ConfigurationPath}' could not be read before saving ({Reason}); it is replaced")]
        private partial void LogUnreadable(string configurationPath, string reason);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "The settings backup could not be written ({Reason}); the save continues")]
        private partial void LogBackupFailed(string reason);
    }
}
