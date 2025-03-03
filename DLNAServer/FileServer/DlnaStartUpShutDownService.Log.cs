namespace DLNAServer.FileServer
{
    public partial class DlnaStartUpShutDownService
    {
        [LoggerMessage(1, LogLevel.Information, "{instance} - Initialized")]
        partial void InformationInstanceInitialized(string instance);
        [LoggerMessage(2, LogLevel.Debug, "{instance} - Terminated")]
        partial void DebugInstanceTerminated(string instance);
        [LoggerMessage(3, LogLevel.Information, "Dlna server name: {serverName}")]
        partial void InformationServerName(string serverName);
        [LoggerMessage(4, LogLevel.Information, "Source folders: {sourceFolders}")]
        partial void InformationSourceFolders(string sourceFolders);
        [LoggerMessage(5, LogLevel.Information, "Extensions: {extensions}")]
        partial void InformationExtensions(string extensions);
        [LoggerMessage(6, LogLevel.Information, "ActualMachineName = '{actualMachineName}', LastMachineName = '{lastMachineName}'")]
        partial void InformationMachineName(string actualMachineName, string? lastMachineName);
        [LoggerMessage(7, LogLevel.Warning, "Database cleared!!!")]
        partial void WarningMachineName();



    }
}
