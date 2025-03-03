using System.Net;

namespace DLNAServer.SOAP.Endpoints
{
    public partial class ContentDirectoryService
    {
        [LoggerMessage(1, LogLevel.Debug, "{operation}(ObjectID: {objectID}, BrowseFlag:{browseFlag}, Filter: {filter}, StartingIndex: {startingIndex}, RequestedCount: {requestedCount}, SortCriteria: {sortCriteria}")]
        partial void DebugBrowseRequestInfo(string operation, string objectID, string browseFlag, string filter, int startingIndex, int requestedCount, string sortCriteria);
        [LoggerMessage(2, LogLevel.Debug, "Started returning browse items: {objectID}")]
        partial void DebugBrowseRequestStart(string objectID);
        [LoggerMessage(3, LogLevel.Information, "Remote IP Address: {remoteIPAddress}:{remotePort}, ObjectID: {objectId}, RequestedCount: {requestedCount}, Starting index: {startingIndex}, Items: {numberReturned} from {totalMatches}, Duration (ms): {duration:0.00}")]
        partial void InformationStartBrowseRequest(IPAddress? remoteIPAddress, int? remotePort, string objectId, int requestedCount, int startingIndex, uint numberReturned, uint totalMatches, double duration);
        [LoggerMessage(4, LogLevel.Debug, "Finished returning browse items: {objectID}")]
        partial void DebugBrowseRequestFinish(string objectID);
        [LoggerMessage(5, LogLevel.Debug, "Error in returning browse items: {objectID}")]
        partial void DebugBrowseRequestError(string objectID);
        [LoggerMessage(6, LogLevel.Warning, "{operation}(CategoryType: {CategoryType}, RID: {RID}, ObjectID: {ObjectID}, PosSecond: {PosSecond}")]
        partial void WarningX_SetBookmarkRequestInfo(string operation, int CategoryType, int RID, string ObjectID, int PosSecond);
    }
}
