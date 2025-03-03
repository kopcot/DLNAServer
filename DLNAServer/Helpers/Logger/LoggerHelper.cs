using System.Net;

namespace DLNAServer.Helpers.Logger
{
    public static class LoggerHelper
    {
        #region general messages
        private static readonly Action<ILogger, string, Exception?> _logTraceMessage =
        LoggerMessage.Define<string>(
            LogLevel.Trace,
            new EventId((int)LogLevel.Trace * -1, "GeneralTrace"),
            "{Message}");
        public static void LogGeneralTraceMessage(this ILogger logger, string message)
        {
            _logTraceMessage(logger, message, null);
        }

        private static readonly Action<ILogger, string, Exception?> _logDebugMessage =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId((int)LogLevel.Debug * -1, "GeneralDebug"),
            "{Message}");
        public static void LogGeneralDebugMessage(this ILogger logger, string message)
        {
            _logDebugMessage(logger, message, null);
        }

        private static readonly Action<ILogger, string, Exception?> _logInformationMessage =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId((int)LogLevel.Information * -1, "GeneralInformation"),
            "{Message}");
        public static void LogGeneralInformationMessage(this ILogger logger, string message)
        {
            _logInformationMessage(logger, message, null);
        }

        private static readonly Action<ILogger, string, Exception?> _logWarningMessage =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId((int)LogLevel.Warning * -1, "GeneralWarning"),
            "{Message}");
        public static void LogGeneralWarningMessage(this ILogger logger, string message)
        {
            _logWarningMessage(logger, message, null);
        }

        private static readonly Action<ILogger, string, string?, Exception?> _logErrorMessage =
        LoggerMessage.Define<string, string?>(
            LogLevel.Error,
            new EventId((int)LogLevel.Error * -1, "GeneralError"),
            "An error occurred: {Message}\n{StackTrace}");
        public static void LogGeneralErrorMessage(this ILogger logger, Exception ex)
        {
            _logErrorMessage(logger, ex.Message, ex.StackTrace, ex);
        }

        private static readonly Action<ILogger, string, Exception?> _logCriticalMessage =
        LoggerMessage.Define<string>(
            LogLevel.Critical,
            new EventId((int)LogLevel.Critical * -1, "GeneralCritical"),
            "{Message}");
        public static void LogGeneralCriticalMessage(this ILogger logger, string message)
        {
            _logCriticalMessage(logger, message, null);
        }
        #endregion general messages

        private static readonly Action<ILogger, string, string, string, string?, string?, Exception?> _logConnectionInformation =
        LoggerMessage.Define<string, string, string, string?, string?>(
            LogLevel.Debug,
            new EventId(1, "ConnectionInformation"),
            "{action}, remote address {remoteIpAddress}, local address {localIpAddress}, path: '{path}',  method: '{method}'");
        public static void LogDebugConnectionInformation(
            ILogger logger,
            string action,
            IPAddress? remoteIpAddress,
            int? remotePort,
            IPAddress? localIpAddress,
            int? localPort,
            string? path = "",
            string? method = "")
        {
            _logConnectionInformation(logger, action, $"{remoteIpAddress}:{remotePort}", $"{localIpAddress}:{localPort}", path, method, null);
        }

        private static readonly Action<ILogger, string, string, string, string?, string, Exception?> _logWarningFallbackError =
        LoggerMessage.Define<string, string, string, string?, string>(
            LogLevel.Warning,
            new EventId(2, "ConnectionInformation"),
            "FallbackError: {action}, remote address {remoteIpAddress}, local address {localIpAddress}, path: '{path}',  method: '{method}'");
        public static void LogWarningFallbackError(
            ILogger logger,
            string action,
            IPAddress? remoteIpAddress,
            int remotePort,
            IPAddress? localIpAddress,
            int localPort,
            string? path = "",
            string method = "")
        {
            _logWarningFallbackError(logger, action, $"{remoteIpAddress}:{remotePort}", $"{localIpAddress}:{localPort}", path, method, null);
        }

        private static readonly Action<ILogger, Exception?> _logWarningOperationCanceled =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(3, "OperationCanceled"),
            "Operation canceled");
        public static void LogWarningOperationCanceled(
            ILogger logger)
        {
            _logWarningOperationCanceled(logger, null);
        }

        private static readonly Action<ILogger, Exception?> _logWarningTaskCanceled =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(4, "TaskCanceled"),
            "Task canceled");
        public static void LogWarningTaskCanceled(
            ILogger logger)
        {
            _logWarningTaskCanceled(logger, null);
        }

    }
}
