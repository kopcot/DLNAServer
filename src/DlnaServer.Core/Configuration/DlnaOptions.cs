namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// Root of the server's configuration, bound from the <c>Dlna</c> configuration section.
    /// Consume through <c>IOptionsMonitor&lt;DlnaOptions&gt;</c> so edits to the backing file take effect without a restart.
    /// </summary>
    public sealed class DlnaOptions
    {
        public const string SectionName = "Dlna";

        public ServerOptions Server { get; set; } = new();
        public LibraryOptions Library { get; set; } = new();
        public ThumbnailOptions Thumbnails { get; set; } = new();
        public FileCacheOptions FileCache { get; set; } = new();
        public CompatibilityOptions Compatibility { get; set; } = new();

        public UploadOptions Upload { get; set; } = new();

        public DatabaseOptions Database { get; set; } = new();
    }
}
