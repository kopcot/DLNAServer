namespace DlnaServer.Host.Configuration
{
    /// <summary>
    /// What the guard had to do to leave a readable configuration file in place.
    /// </summary>
    public enum ConfigurationFileStatus
    {
        /// <summary>
        /// The file was present and well-formed; nothing was changed.
        /// </summary>
        Valid = 1,

        /// <summary>
        /// No file was present, so one was written with default values.
        /// </summary>
        Created = 2,

        /// <summary>
        /// The file could not be read or parsed. It was backed up and replaced with defaults.
        /// </summary>
        Replaced = 3,

        /// <summary>
        /// The file reads and parses, but carries no <c>Dlna</c> section, so every setting in it is being
        /// ignored. Left exactly as it was found.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="Replaced"/> on purpose: nothing is wrong with the file as a file, so
        /// overwriting it would destroy settings the operator may only have mis-shaped. The likeliest
        /// cause is the reference server's flat <c>config.json</c>, whose keys sit at the root - this
        /// server's schema is grouped under <c>Dlna</c> and the two are not compatible.
        /// </remarks>
        Unrecognised = 4,
    }
}
