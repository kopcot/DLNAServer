using System.Diagnostics.CodeAnalysis;
using System.ServiceModel;

namespace DlnaServer.Upnp.Soap.ContentDirectory
{
    /// <summary>
    /// The ContentDirectory service: how a renderer lists and navigates the library.
    /// </summary>
    /// <remarks>
    /// Action names, argument names and their order are fixed by the SCPD document served at
    /// <c>/SCPD/contentDirectory.xml</c>. A renderer builds its SOAP request from that document, so the
    /// two must agree exactly.
    /// </remarks>
    [ServiceContract(Namespace = Constants.UpnpServices.ServiceType.ContentDirectory)]
    [SuppressMessage(
        "Naming",
        "CA1707:Identifiers should not contain underscores",
        Justification = "X_GetFeatureList and X_SetBookmark are the vendor-extension action names "
            + "/SCPD/contentDirectory.xml advertises and Samsung televisions send. The underscore is "
            + "part of the wire contract.")]
    public interface IContentDirectoryService
    {
        /// <summary>
        /// <b>Browse</b><br />
        /// Lists the children of a container, or the metadata of a single object.
        /// </summary>
        /// <param name="ObjectID">Identifier to browse. <c>0</c>, or anything unrecognised, means the root.</param>
        /// <param name="BrowseFlag"><c>BrowseDirectChildren</c> or <c>BrowseMetadata</c>.</param>
        /// <param name="Filter">Comma-separated properties to include, or <c>*</c> for all.</param>
        /// <param name="StartingIndex">Zero-based offset into the child list.</param>
        /// <param name="RequestedCount">Maximum items to return. Zero means all.</param>
        /// <param name="SortCriteria">Signed, comma-separated sort keys such as <c>+dc:title</c>.</param>
        /// <remarks>
        /// Parameter names are PascalCase, against the usual camelCase convention, because the name IS
        /// the contract: SoapCore binds each argument from the SOAP body element of the same name, and
        /// renderers send <c>&lt;ObjectID&gt;</c> as the specification requires. camelCase names bind
        /// nothing and every argument arrives null.
        /// </remarks>
        [OperationContract(Name = "Browse")]
        Task<BrowseResponse> Browse(
            string ObjectID,
            string BrowseFlag,
            string Filter,
            int StartingIndex,
            int RequestedCount,
            string SortCriteria);

        /// <summary>
        /// <b>GetSystemUpdateID</b><br />
        /// A counter a renderer polls to notice that the library changed.
        /// </summary>
        [OperationContract(Name = "GetSystemUpdateID")]
        GetSystemUpdateIdResponse GetSystemUpdateID();

        /// <summary>
        /// <b>GetSearchCapabilities</b><br />
        /// Which properties may be searched. <c>*</c> claims all of them.
        /// </summary>
        [OperationContract(Name = "GetSearchCapabilities")]
        GetSearchCapabilitiesResponse GetSearchCapabilities();

        /// <summary>
        /// <b>GetSortCapabilities</b><br />
        /// Which properties may be sorted on.
        /// </summary>
        [OperationContract(Name = "GetSortCapabilities")]
        GetSortCapabilitiesResponse GetSortCapabilities();

        /// <summary>
        /// <b>IsAuthorized</b><br />
        /// Microsoft extension. Always authorised: this is a read-only server on a trusted LAN.
        /// </summary>
        [OperationContract(Name = "IsAuthorized")]
        IsAuthorizedResponse IsAuthorized(string DeviceID);

        /// <summary>
        /// <b>X_GetFeatureList</b><br />
        /// Samsung extension. Vendor container roots by content type; none are offered.
        /// </summary>
        /// <remarks>
        /// Advertised in <c>/SCPD/contentDirectory.xml</c>, so a Samsung television calls it before
        /// browsing and an unimplemented action answers with a fault.
        /// </remarks>
        [OperationContract(Name = "X_GetFeatureList")]
        XGetFeatureListResponse X_GetFeatureList();

        /// <summary>
        /// <b>X_SetBookmark</b><br />
        /// Samsung extension. A television reporting where playback stopped, so it can resume later.
        /// </summary>
        /// <remarks>
        /// Accepted and discarded - nothing here stores playback state. Also advertised in the SCPD.
        /// </remarks>
        [OperationContract(Name = "X_SetBookmark")]
        XSetBookmarkResponse X_SetBookmark(int CategoryType, int RID, string ObjectID, int PosSecond);
    }
}
