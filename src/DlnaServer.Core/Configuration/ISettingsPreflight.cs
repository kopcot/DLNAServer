namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// Answers whether the server would accept a candidate configuration, as startup would see it.
    /// </summary>
    /// <remarks>
    /// A seam that exists so the admin UI can reach the host: startup fills omitted options in before
    /// validation runs, and the code that does that lives in <c>DlnaServer.Host</c>, which the admin
    /// project cannot reference. Validating the raw edit instead made the Settings page refuse
    /// configurations the server boots with quite happily - clearing the source folders reported "at
    /// least one source folder must be listed" where startup falls back to the application's own folder.
    /// </remarks>
    public interface ISettingsPreflight
    {
        /// <summary>
        /// Returns one message per reason the server would refuse to start, and nothing when it would start.
        /// </summary>
        /// <remarks>
        /// The candidate is not modified - the caller goes on to save exactly what it passed in. That
        /// matters: the defaults seed entries into the excluded-folder list, and applying them to the
        /// object the page is about to write is the shape that once grew <c>config.json</c> by two
        /// entries on every save.
        /// </remarks>
        IReadOnlyList<string> Validate(DlnaOptions candidate);
    }
}
