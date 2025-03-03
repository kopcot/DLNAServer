using System.Net;

namespace DLNAServer.Controllers.Media
{
    public partial class FileServerController
    {
        [LoggerMessage(1, LogLevel.Warning, "File with id '{fileGuid}' not found")]
        partial void WarningFileNotFound(string fileGuid);
        [LoggerMessage(2, LogLevel.Warning, "Thumbnail file with id '{thumbnailFileGuid}' not found")]
        partial void WarningThumbnailFileNotFound(string thumbnailFileGuid);
        [LoggerMessage(3, LogLevel.Information, "Remote IP Address: {remoteIpAddress}:{remotePort}, Serving file from disk = {fileFullPath}, {contextType} , {size:0.00}MB")]
        partial void InformationServingFileFromDisk(IPAddress? remoteIpAddress, int? remotePort, string fileFullPath, string contextType, double size);
        [LoggerMessage(4, LogLevel.Information, "Remote IP Address: {remoteIpAddress}:{remotePort}, Serving file from cache = {fileFullPath}, {contextType} , {size:0.00}MB")]
        partial void InformationServingFileFromCache(IPAddress? remoteIpAddress, int? remotePort, string fileFullPath, string contextType, double size);
        [LoggerMessage(5, LogLevel.Information, "Remote IP Address: {remoteIpAddress}:{remotePort}, Serving thumbnail file from disk = {fileFullPath}, {contextType} , {size:0.00}kB")]
        partial void InformationServingThumbnailFileFromDisk(IPAddress? remoteIpAddress, int? remotePort, string fileFullPath, string contextType, double size);
        [LoggerMessage(6, LogLevel.Information, "Remote IP Address: {remoteIpAddress}:{remotePort}, Serving thumbnail file from cache = {fileFullPath}, {contextType} , {size:0.00}kB")]
        partial void InformationServingThumbnailFileFromCache(IPAddress? remoteIpAddress, int? remotePort, string fileFullPath, string contextType, double size);
        [LoggerMessage(7, LogLevel.Information, "Remote IP Address: {remoteIpAddress}:{remotePort}, Serving thumbnail file from database = {fileFullPath}, {contextType} , {size:0.00}kB")]
        partial void InformationServingThumbnailFileFromDatabase(IPAddress? remoteIpAddress, int? remotePort, string fileFullPath, string contextType, double size);
        [LoggerMessage(8, LogLevel.Debug, "Unable to cache thumbnail file, serving from disk.")]
        partial void DebugUnableToCacheThumbnailFile();
        [LoggerMessage(9, LogLevel.Debug, "Thumbnail file path: {filePath}")]
        partial void DebugThumbnailFilePath(string filePath);
        [LoggerMessage(10, LogLevel.Debug, "File path: {filePath}")]
        partial void DebugFilePath(string filePath);
    }
}
