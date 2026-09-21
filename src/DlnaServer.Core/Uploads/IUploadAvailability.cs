namespace DlnaServer.Core.Uploads
{
    /// <summary>
    /// Whether this running server can accept an upload.
    /// </summary>
    /// <remarks>
    /// Not the same question as <c>Dlna.Upload.Enabled</c>, and the difference is the whole reason this
    /// exists: the endpoint is created while the application is being built, so a setting saved since
    /// then has not taken effect. The page asks this to decide whether to offer a form, and asks the
    /// setting to decide whether to say "restart to apply".
    /// </remarks>
    public interface IUploadAvailability
    {
        /// <summary>
        /// True when the upload endpoint was created at startup.
        /// </summary>
        bool IsAcceptingUploads { get; }
    }
}
