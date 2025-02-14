using DLNAServer.SOAP.Endpoints.Responses.ContentDirectory;
using DLNAServer.Types.DLNA;
using System.ServiceModel;

namespace DLNAServer.SOAP.Endpoints.Interfaces
{
    [ServiceContract(Namespace = XmlNamespaces.NS_ServiceType_ContentDirectory)]
    public interface IContentDirectoryService
    {
        [OperationContract(Name = "Browse")]
        Task<Browse> Browse(string ObjectID, string BrowseFlag, string Filter, int StartingIndex, int RequestedCount, string SortCriteria);

        [OperationContract(Name = "GetSearchCapabilities", AsyncPattern = true)]
        Task<GetSearchCapabilities> GetSearchCapabilities();

        [OperationContract(Name = "GetSortCapabilities")]
        Task<GetSortCapabilities> GetSortCapabilities();

        [OperationContract(Name = "IsAuthorized")]
        Task<IsAuthorized> IsAuthorized(string DeviceID);

        [OperationContract(Name = "GetSystemUpdateID")]
        Task<GetSystemUpdateID> GetSystemUpdateID();

        [OperationContract(Name = "X_GetFeatureList")]
        Task<X_GetFeatureList> X_GetFeatureList();

        [OperationContract(Name = "X_SetBookmark")]
        Task<X_SetBookmark> X_SetBookmark(int CategoryType, int RID, string ObjectID, int PosSecond);
    }
}
