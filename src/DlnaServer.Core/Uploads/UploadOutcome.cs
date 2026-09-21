namespace DlnaServer.Core.Uploads
{
    /// <summary>
    /// What became of one file in an upload.
    /// </summary>
    public enum UploadOutcome
    {
        /// <summary>
        /// Written, and nothing was there before.
        /// </summary>
        Uploaded = 1,

        /// <summary>
        /// Written over a file that was already there, because the operator asked for that.
        /// </summary>
        Overwritten = 2,

        /// <summary>
        /// Left alone because a file of that name was already there.
        /// </summary>
        Skipped = 3,

        /// <summary>
        /// Refused or interrupted. The reason says which.
        /// </summary>
        Failed = 4,
    }
}
