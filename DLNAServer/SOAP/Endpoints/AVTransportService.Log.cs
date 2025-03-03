namespace DLNAServer.SOAP.Endpoints
{
    public partial class AVTransportService
    {
        [LoggerMessage(1, LogLevel.Warning, "{operation}(InstanceID: {instanceID}, AVTransportURI: {currentURI}, AVTransportURIMetaData: {currentURIMetaData})")]
        partial void WarningSetAVTransportURIRequestInfo(string operation, int instanceID, string currentURI, string currentURIMetaData);
        [LoggerMessage(2, LogLevel.Warning, "{operation}(InstanceID: {instanceID}, Speed: {speed})")]
        partial void WarningPlayRequestInfo(string operation, int instanceID, string speed);
        [LoggerMessage(3, LogLevel.Warning, "{operation}(InstanceID: {instanceID})")]
        partial void WarningPauseRequestInfo(string operation, int instanceID);
        [LoggerMessage(4, LogLevel.Warning, "{operation}(InstanceID: {instanceID})")]
        partial void WarningStopRequestInfo(string operation, int instanceID);
        [LoggerMessage(5, LogLevel.Warning, "{operation}(InstanceID: {InstanceID}, SeekMode: {SeekMode}, Target: {Target})")]
        partial void WarningSeekRequestInfo(string operation, int InstanceID, string SeekMode, string Target);
        [LoggerMessage(6, LogLevel.Warning, "{operation}(InstanceID: {instanceID})")]
        partial void WarningGetTransportInfoRequestInfo(string operation, int instanceID);
        [LoggerMessage(7, LogLevel.Warning, "{operation}(InstanceID: {instanceID})")]
        partial void WarningGetPositionInfoRequestInfo(string operation, int instanceID);
    }
}
