using DLNAServer.Database.Entities;
using DLNAServer.Types.DLNA;

namespace DLNAServer.SOAP.Endpoints.Responses.ContentDirectory.Mapping
{
    public static class BrowseItemMapper
    {
        private const string rootParentId = "0";
        #region Container
        public static BrowseItem MapContainer(this DirectoryEntity directory, string ipEndpoint, bool isRootFolder)
        {
            try
            {
                return new BrowseItem()
                {
                    Title = GetTitle(directory, isRootFolder),
                    ObjectID = GetObjectID(directory),
                    ParentID = GetParentID(directory, isRootFolder),
                    Class = GetUpnpClass(directory),
                    ThumbnailUri = GetThumbnailUri(directory, ipEndpoint),
                    Icon = GetThumbnailUri(directory, ipEndpoint),
                    Searchable = "1",
                };
            }
            catch
            {
                throw;
            }
        }
        private static string GetTitle(DirectoryEntity directory, bool isRootFolder) => isRootFolder
            ? $"{directory.Directory} ({directory.ParentDirectory?.DirectoryFullPath})"
            : directory.Directory;
        //TODO
        private static string GetUpnpClass(DirectoryEntity directory) => DlnaItemClass.Container.ToItemClass();
        private static string GetObjectID(DirectoryEntity directory) => directory.Id.ToString();
        private static string GetParentID(DirectoryEntity directory, bool isRootFolder) => isRootFolder ? rootParentId : directory.ParentDirectory?.Id.ToString() ?? rootParentId;
        private static string GetThumbnailUri(DirectoryEntity directory, string ipEndpoint) => $"http://{ipEndpoint}/icon/folder.jpg";
        #endregion
        #region Item
        public static BrowseItem MapItem(this FileEntity file, string ipEndpoint, bool isRootFolder)
        {
            try
            {
                return new BrowseItem()
                {
                    Title = GetTitle(file, isRootFolder),
                    ObjectID = GetObjectID(file),
                    ParentID = GetParentID(file, isRootFolder),
                    Class = GetUpnpClass(file),
                    ThumbnailUri = GetResourceThumbnailUrl(file, ipEndpoint),
                    Icon = GetResourceThumbnailUrl(file, ipEndpoint),
                    Date = GetDate(file),
                    VideoCodec = GetVideoCodec(file),
                    AudioCodec = GetAudioCodec(file),
                    Resource =
                    [
                        new()
                    {
                        ProtocolInfo = GetResourceProtocolInfo(file),
                        Url = GetResourceUrl(file, ipEndpoint),
                        SizeInBytes = GetResourceSize(file),
                        Duration = GetResourceDuration(file),
                        Resolution = GetResourceResolution(file),
                        Bitrate = GetResourceBitrate(file),
                        AudioChannels = GetAudioChannels(file),
                        TypeOfMedia =  GetTypeOfMedia(file),
                    }
                    ],
                    ResourceThumbnail = GetResourceThumbnailUrl(file, ipEndpoint) == null ? null
                        : new()
                        {
                            ProtocolInfo = GetResourceThumbnailProtocolInfo(file),
                            Url = GetResourceThumbnailUrl(file, ipEndpoint)!,
                            SizeInBytes = GetResourceThumbnailSize(file),
                        }
                };
            }
            catch
            {
                throw;
            }
        }
        private static string GetTitle(FileEntity file, bool isRootFolder) => isRootFolder
            ? $"{file.Title} ({file.Folder})"
            : file.Title;
        private static string GetUpnpClass(FileEntity file) => file.UpnpClass.ToItemClass();
        private static string GetObjectID(FileEntity file) => file.Id.ToString();
        private static string GetParentID(FileEntity file, bool isRootFolder) => isRootFolder ? rootParentId : file.Directory?.Id.ToString() ?? rootParentId;
        private static string GetDate(FileEntity file) => file.FileCreateDate.ToString("O");
        private static string GetResourceUrl(FileEntity file, string ipEndpoint) => $"http://{ipEndpoint}/fileserver/file/{file.Id}";
        private static string GetResourceProtocolInfo(FileEntity file)
        {
            return $"http-get:*:" +
                    $"{file.FileDlnaMime.ToMimeString()}:" +
                    $"DLNA.ORG_PN={file.FileDlnaProfileName
                        ?? file.FileExtension.ToUpper().Replace(".", "")};" +
                    $"DLNA.ORG_OP={ProtocolInfo.FlagsToString(ProtocolInfo.DlnaOrgOperation.TimeSeekSupported)};" +
                    $"DLNA.ORG_CI={ProtocolInfo.EnumToString(ProtocolInfo.DlnaOrgContentIndex.NoSpecificIndex)};" +
                    $"DLNA.ORG_FLAGS={(file.UpnpClass.ToDlnaMedia() == DlnaMedia.Image
                        ? ProtocolInfo.DefaultFlagsInteractive
                        : ProtocolInfo.DefaultFlagsStreaming)}";
        }
        private static string? GetResourceThumbnailUrl(FileEntity file, string ipEndpoint)
        {
            if (file.ThumbnailId.HasValue)
            {
                return $"http://{ipEndpoint}/fileserver/thumbnail/{file.ThumbnailId}";
            }
            else
            {
                return file.UpnpClass.ToDlnaMedia() switch
                {
                    DlnaMedia.Image => $"http://{ipEndpoint}/fileserver/file/{file.Id}",
                    DlnaMedia.Video => $"http://{ipEndpoint}/icon/fileMovie.jpg",
                    DlnaMedia.Audio => $"http://{ipEndpoint}/icon/fileAudio.jpg",
                    _ => null,
                };
            }
        }
        private static string GetResourceThumbnailProtocolInfo(FileEntity file)
        {
            return $"http-get:*:" +
                $"{file.Thumbnail?.ThumbnailFileDlnaMime.ToMimeString() ?? "*"}:" +
                $"DLNA.ORG_PN={(file.Thumbnail?.ThumbnailFileDlnaMime != null && file.Thumbnail?.ThumbnailFileDlnaMime != DlnaMime.Undefined ? file.Thumbnail?.ThumbnailFileDlnaProfileName
                    : file.FileDlnaMime.ToDlnaMedia() == DlnaMedia.Image ? file.FileDlnaProfileName
                    : file.FileDlnaMime.ToDlnaMedia() == DlnaMedia.Audio ? DlnaMime.ImageJpeg.ToMainProfileNameString()
                    : file.Thumbnail?.ThumbnailFileExtension?.ToUpper().Replace(".", ""))
                    ?? ""};" +
                $"DLNA.ORG_OP={ProtocolInfo.FlagsToString(ProtocolInfo.DlnaOrgOperation.None)};" +
                $"DLNA.ORG_CI={ProtocolInfo.EnumToString(ProtocolInfo.DlnaOrgContentIndex.Thumbnail)};" +
                $"DLNA.ORG_FLAGS={ProtocolInfo.DefaultFlagsInteractive}";
        }
        private static long GetResourceSize(FileEntity file) => file.FileSizeInBytes;
        private static long GetResourceThumbnailSize(FileEntity file) => file.Thumbnail?.ThumbnailFileSizeInBytes ?? 0;
        private static string? GetResourceDuration(FileEntity file)
        {
            return file.UpnpClass.ToDlnaMedia() switch
            {
                DlnaMedia.Video => file.VideoMetadata?.Duration.HasValue == true
                    ? FormatDuration(file.VideoMetadata.Duration.Value)
                    : null,
                DlnaMedia.Audio => file.AudioMetadata?.Duration.HasValue == true
                    ? FormatDuration(file.AudioMetadata.Duration.Value)
                    : null,
                _ => null,
            };

            static string FormatDuration(TimeSpan duration)
            {
                return $"{(int)(duration.TotalHours):00}:{duration.Minutes:00}:{duration.Seconds:00}.{duration.Milliseconds:000}";
            }
        }
        private static string? GetResourceResolution(FileEntity file)
        {
            switch (file.UpnpClass.ToDlnaMedia())
            {
                case DlnaMedia.Video:
                    {
                        if (file.VideoMetadata is MediaVideoEntity metadata &&
                        metadata.Height.HasValue &&
                            metadata.Width.HasValue)
                        {
                            return $"{metadata.Width}x{metadata.Height}";
                        }
                    }
                    break;
            }
            return null;
        }
        private static string? GetResourceBitrate(FileEntity file)
        {
            switch (file.UpnpClass.ToDlnaMedia())
            {
                case DlnaMedia.Video:
                    {
                        if (file.VideoMetadata is MediaVideoEntity metadata &&
                            metadata.Bitrate.HasValue)
                        {
                            return $"{metadata.Bitrate}";
                        }
                    }
                    break;
                case DlnaMedia.Audio:
                    {
                        if (file.AudioMetadata is MediaAudioEntity metadata &&
                            metadata.Bitrate.HasValue)
                        {
                            return $"{metadata.Bitrate}";
                        }
                    }
                    break;
            }
            return null;
        }
        private static string? GetVideoCodec(FileEntity file)
        {
            switch (file.UpnpClass.ToDlnaMedia())
            {
                case DlnaMedia.Video:
                    {
                        if (file.VideoMetadata is MediaVideoEntity metadata &&
                            !string.IsNullOrWhiteSpace(metadata.Codec))
                        {
                            return $"{metadata.Codec}";
                        }
                    }
                    break;
            }
            return null;
        }
        private static string? GetAudioCodec(FileEntity file)
        {
            switch (file.UpnpClass.ToDlnaMedia())
            {
                case DlnaMedia.Video:
                    {
                        if (file.AudioMetadata is MediaAudioEntity metadata &&
                            !string.IsNullOrWhiteSpace(metadata.Codec))
                        {
                            return $"{metadata.Codec}";
                        }
                    }
                    break;
                case DlnaMedia.Audio:
                    {
                        if (file.AudioMetadata is MediaAudioEntity metadata &&
                            !string.IsNullOrWhiteSpace(metadata.Codec))
                        {
                            return $"{metadata.Codec}";
                        }
                    }
                    break;
            }
            return null;
        }
        private static string? GetAudioChannels(FileEntity file)
        {
            switch (file.UpnpClass.ToDlnaMedia())
            {
                case DlnaMedia.Video:
                    {
                        if (file.AudioMetadata is MediaAudioEntity metadata &&
                            metadata.Channels.HasValue)
                        {
                            return $"{metadata.Channels}";
                        }
                    }
                    break;
                case DlnaMedia.Audio:
                    {
                        if (file.AudioMetadata is MediaAudioEntity metadata &&
                            metadata.Channels.HasValue)
                        {
                            return $"{metadata.Channels}";
                        }
                    }
                    break;
            }
            return null;
        }
        private static string? GetTypeOfMedia(FileEntity file)
        {
            switch (file.UpnpClass.ToDlnaMedia())
            {
                case DlnaMedia.Video:
                    return "video";
                case DlnaMedia.Audio:
                    return "audio";
                case DlnaMedia.Image:
                    return "image";
                case DlnaMedia.Subtitle:
                    return "subtitles";
                default:
                    break;
            }
            return null;
        }

        #endregion
    }
}
