using DLNAServer.Helpers.Serializations;
using DLNAServer.Types.DLNA;
using System.Reflection;
using System.Text.Json.Serialization;

namespace DLNAServer.Configuration
{
    public class ServerConfig : IDisposable
    {
        public ServerConfig()
        {
            InstanceNotLoadedFromFile = true;
        }
        private static ServerConfig? instance;
        public static ServerConfig Instance => instance ??= LoadOrCreateNew();

        private static ServerConfig LoadOrCreateNew()
        {
            return JsonSerialization.LoadFromJsonOrCreateNew(ConfigurationJsonFile, static () => new ServerConfig());
        }

        #region Server internals, not for saving 
        [JsonIgnore]
        public static bool DlnaServerRestart { get; set; } = false;
        [JsonIgnore]
        public static string ConfigurationJsonFile => Path.Combine(Directory.GetCurrentDirectory(), "Resources", "configuration", "config.json");
        [JsonIgnore]
        public string DlnaServerName => GenerateServerSignature();
        [JsonIgnore]
        public string DlnaServerManufacturerName => $"Kopco";
        [JsonIgnore]
        public string DlnaServerManufacturerUrl => $"mailto:kopco.t@gmail.com";
        [JsonIgnore]
        public bool DlnaServerDebugMode { get; set; } = false;
        [JsonIgnore]
        public bool InstanceNotLoadedFromFile { get; set; } = false;
        #endregion 
        // General 
        public uint DlnaServerPort { get; set; } = 26851;
        public string DlnaServerFriendlyName { get; set; } = $"ZEN DLNA Server ({Environment.MachineName})";
        public string DlnaServerModelName { get; set; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? $"0.0.0.0 (-9999)";
        public uint DlnaServerMaxLogMessagesCount { get; set; } = (uint)short.MaxValue;
        public bool AlwaysRecreateDatabaseAtStart { get; set; } = false;
        public bool IgnoreRequestedCountAttributeFromRequest { get; set; } = false;
        // FileServer
        public bool UseMemoryCacheForStreamingFile { get; set; } = true;
        public bool GenerateMetadataForLocalMovies { get; set; } = true;
        public bool GenerateMetadataForLocalAudio { get; set; } = true;
        public bool GenerateMetadataForLocalImages { get; set; } = true;
        public bool GenerateThumbnailsForLocalMovies { get; set; } = true;
        public bool StoreThumbnailsForLocalMoviesInDatabase { get; set; } = true;
        public bool GenerateThumbnailsForLocalImages { get; set; } = true;
        public bool StoreThumbnailsForLocalImagesInDatabase { get; set; } = true;
        public uint MaxWidthForThumbnails { get; set; } = 480;
        public uint MaxHeightForThumbnails { get; set; } = 360;
        public ushort QualityForThumbnails { get; set; } = 50;
        public DlnaMime DefaultDlnaMimeForVideoThumbnails { get; set; } = DlnaMime.ImageJpeg;
        public DlnaMime DefaultDlnaMimeForImageThumbnails { get; set; } = DlnaMime.ImageJpeg;
        public ushort MaxUseMemoryCacheInMBytes { get; set; } = 10_240;
        public ushort MaxSizeOfFileForUseMemoryCacheInMBytes { get; set; } = 1_024;
        public bool GenerateMetadataAndThumbnailsAfterAdding { get; set; } = true;
        public uint StoreFileInMemoryCacheAfterLoadInMinute { get; set; } = 10;
        public uint CountOfFilesByLastAddedToDb { get; set; } = 30;
        public Dictionary<string, KeyValuePair<DlnaMime, string?>> MediaFileExtensions { get; set; } = new Dictionary<string, KeyValuePair<DlnaMime, string?>>()
            {
                {".mp4",  new KeyValuePair<DlnaMime, string?>(DlnaMime.VideoMp4,            DlnaMime.VideoMp4.ToMainProfileNameString()         )},
                {".avi",  new KeyValuePair<DlnaMime, string?>(DlnaMime.VideoXMsvideo,       DlnaMime.VideoXMsvideo.ToMainProfileNameString()    )},
                {".mkv",  new KeyValuePair<DlnaMime, string?>(DlnaMime.VideoXMatroska,      DlnaMime.VideoXMatroska.ToMainProfileNameString()   )},
                {".mov",  new KeyValuePair<DlnaMime, string?>(DlnaMime.VideoQuicktime,      DlnaMime.VideoQuicktime.ToMainProfileNameString()   )},
                {".wmv",  new KeyValuePair<DlnaMime, string?>(DlnaMime.VideoXMswmv,         DlnaMime.VideoXMswmv.ToMainProfileNameString()      )},
                {".flv",  new KeyValuePair<DlnaMime, string?>(DlnaMime.VideoXFlv,           DlnaMime.VideoXFlv.ToMainProfileNameString()        )},
                {".m4v",  new KeyValuePair<DlnaMime, string?>(DlnaMime.VideoMpeg,           DlnaMime.VideoMpeg.ToMainProfileNameString()        )},
                {".3gp",  new KeyValuePair<DlnaMime, string?>(DlnaMime.Video3gpp,           DlnaMime.Video3gpp.ToMainProfileNameString()        )},
                {".mp3",  new KeyValuePair<DlnaMime, string?>(DlnaMime.AudioMp4,            DlnaMime.AudioMp4.ToMainProfileNameString()         )}, // !!! LG-TV take 'audio/mp4' instead of 'audio/mpeg3'
                {".png",  new KeyValuePair<DlnaMime, string?>(DlnaMime.ImagePng,            DlnaMime.ImagePng.ToMainProfileNameString()         )},
                {".jpg",  new KeyValuePair<DlnaMime, string?>(DlnaMime.ImageJpeg,           DlnaMime.ImageJpeg.ToMainProfileNameString()        )},
                {".jpeg", new KeyValuePair<DlnaMime, string?>(DlnaMime.ImageJpeg,           DlnaMime.ImageJpeg.ToMainProfileNameString()        )},
            };
        [JsonIgnore] // not implemented
        public Dictionary<string, KeyValuePair<DlnaMime, string?>> SubtitleFileExtensions { get; set; } = new Dictionary<string, KeyValuePair<DlnaMime, string?>>()
            {
                {".srt",  new KeyValuePair<DlnaMime, string?>(DlnaMime.SubtitleSubrip,      DlnaMime.SubtitleSubrip.ToMainProfileNameString()   )},
                {".sub",  new KeyValuePair<DlnaMime, string?>(DlnaMime.SubtitleMicroDVD,    DlnaMime.SubtitleMicroDVD.ToMainProfileNameString() )},
            };
        public List<string> SourceFolders { get; set; } = Environment.MachineName.Contains("NAS")
            ? [
                "/share/Media",
            ]
            : [
                Directory.GetCurrentDirectory(),
            ];
        public List<string> ExcludeFolders { get; set; } =
            [
                ".@__thumb",
                "@Recycle",
            ];
        /// <summary>
        /// unable to create system folders, like '.@__thumb', if they are disabled to create by system
        /// </summary>
        public string SubFolderForThumbnail { get; set; } = ".@__thumb";

        private static string GenerateServerSignature()
        {
            var os = Environment.OSVersion;
            var platform = os.Platform.ToString();
            switch (os.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    platform = "WIN";
                    break;
                case PlatformID.Unix:
                    platform = "Linux";
                    break;
                case PlatformID.Xbox:
                case PlatformID.MacOSX:
                case PlatformID.Other:
                default:
                    break;
            }
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var versionMajor = version?.Major ?? -1;
            var versionMinor = version?.Minor ?? -1;
            var bitVersion = IntPtr.Size * 8;
            var signature = $"{platform}/{bitVersion}bit/{os.Version.Major}.{os.Version.Minor} UPnP/1.0 DLNADOC/1.5 zen_dlna/{versionMajor}.{versionMinor}";
            return signature;
        }

        #region Dispose
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                instance = null;

                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ServerConfig()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion 
    }
}
