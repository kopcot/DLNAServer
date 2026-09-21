namespace DlnaServer.Core.Uploads
{
    /// <summary>
    /// Holds an upload report between the post that produced it and the page that shows it.
    /// </summary>
    /// <remarks>
    /// In memory and short-lived on purpose. The post answers with a redirect rather than a page - so a
    /// reload does not upload everything a second time - and the report has to survive only that one hop.
    /// The permanent record is <c>logs/uploadSecurity.log</c>, which holds more than this does.
    /// </remarks>
    public interface IUploadReportStore
    {
        /// <summary>
        /// Keeps a report for the short period the redirect needs.
        /// </summary>
        void Add(UploadReport report);

        /// <summary>
        /// The report with that identifier, or null once it has lapsed or if it never existed.
        /// </summary>
        UploadReport? Find(Guid id);
    }
}
