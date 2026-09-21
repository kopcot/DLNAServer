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
    }
}
