using DLNAServer.Database.Entities;
using DLNAServer.Types.DLNA;
using System.Globalization;
using System.Text;

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
                    Title = GetTitle(ref directory, isRootFolder),
                    ObjectID = GetObjectID(ref directory),
                    ParentID = GetParentID(ref directory, isRootFolder),
                    Class = GetUpnpClass(ref directory),
                    ThumbnailUri = GetThumbnailUri(ref directory, ipEndpoint),
                    Icon = GetThumbnailUri(ref directory, ipEndpoint),
                    Searchable = "1",
                };
            }
            catch
            {
                throw;
            }
        }
        private static string GetTitle(ref readonly DirectoryEntity directory, bool isRootFolder)
        {
            StringBuilder sb = new();
            if (isRootFolder)
            {
                return sb.Append(directory.Directory)
                        .Append(" (")
                        .Append(directory.ParentDirectory?.DirectoryFullPath)
                        .Append(')')
                        .ToString();
            }
            else
            {
                return sb.Append(directory.Directory)
                    .ToString();
            }
        }

        //TODO
        private static string GetUpnpClass(ref readonly DirectoryEntity directory) => DlnaItemClass.Container.ToItemClass();
        private static string GetObjectID(ref readonly DirectoryEntity directory) => directory.Id.ToString();
        private static string GetParentID(ref readonly DirectoryEntity directory, bool isRootFolder) => isRootFolder ? rootParentId : directory.ParentDirectory?.Id.ToString() ?? rootParentId;
        private static string GetThumbnailUri(ref readonly DirectoryEntity directory, string ipEndpoint)
        {
            StringBuilder sb = new(50);
            return sb.Append("http://")
                .Append(ipEndpoint)
                .Append("/icon/folder.jpg")
                .ToString();
        }
        #endregion
        #region Item
        public static BrowseItem MapItem(this FileEntity file, string ipEndpoint, bool isRootFolder)
        {
            try
            {
                return new BrowseItem()
                {
                    Title = GetTitle(ref file, isRootFolder),
                    ObjectID = GetObjectID(ref file),
                    ParentID = GetParentID(ref file, isRootFolder),
                    Class = GetUpnpClass(ref file),
                    ThumbnailUri = GetResourceThumbnailUrl(ref file, ipEndpoint),
                    Icon = GetResourceThumbnailUrl(ref file, ipEndpoint),
                    Date = GetDate(ref file),
                    VideoCodec = GetVideoCodec(ref file),
                    AudioCodec = GetAudioCodec(ref file),
                    Resource =
                    [
                        new()
                    {
                        ProtocolInfo = GetResourceProtocolInfo(ref file),
                        Url = GetResourceUrl(ref file, ipEndpoint),
                        SizeInBytes = GetResourceSize(ref file),
                        Duration = GetResourceDuration(ref file),
                        Resolution = GetResourceResolution(ref file),
                        Bitrate = GetResourceBitrate(ref file),
                        AudioChannels = GetAudioChannels(ref file),
                        TypeOfMedia =  GetTypeOfMedia(ref file),
                    }
                    ],
                    ResourceThumbnail = GetResourceThumbnailUrl(ref file, ipEndpoint) == null ? null
                        : new()
                        {
                            ProtocolInfo = GetResourceThumbnailProtocolInfo(ref file),
                            Url = GetResourceThumbnailUrl(ref file, ipEndpoint)!,
                            SizeInBytes = GetResourceThumbnailSize(ref file),
                        }
                };
            }
            catch
            {
                throw;
            }
        }
        private static string GetTitle(ref readonly FileEntity file, bool isRootFolder)
        {

            StringBuilder sb = new();
            if (isRootFolder)
            {
                return sb.Append(file.Title)
                .Append(" (")
                        .Append(file.Folder)
                        .Append(')')
                        .ToString();
            }
            else
            {
                return sb.Append(file.Title)
                    .ToString();
            }
        }

        private static string GetUpnpClass(ref readonly FileEntity file) => file.UpnpClass.ToItemClass();
        private static string GetObjectID(ref readonly FileEntity file) => file.Id.ToString();
        private static string GetParentID(ref readonly FileEntity file, bool isRootFolder) => isRootFolder ? rootParentId : file.Directory?.Id.ToString() ?? rootParentId;
        private static string GetDate(ref readonly FileEntity file) => file.FileCreateDate.ToString("O");
        private static string GetResourceUrl(ref readonly FileEntity file, string ipEndpoint)
        {
            StringBuilder sb = new(50);
            return sb.Append("http://")
                .Append(ipEndpoint)
                .Append("/fileserver/file/")
                .Append(file.Id.ToString())
                .ToString();
        }

        private static string GetResourceProtocolInfo(ref readonly FileEntity file)
        {
            StringBuilder sb = new(50);
            _ = sb.Append("http-get:*:")
                .Append(file.FileDlnaMime.ToMimeString())
                .Append(':');
            _ = sb.Append("DLNA.ORG_PN=")
                .Append(file.FileDlnaProfileName
                    ?? file.FileExtension.ToUpper().Replace(".", ""))
                .Append(';');
            _ = sb.Append("DLNA.ORG_OP=")
                .Append(ProtocolInfo.FlagsToString(ProtocolInfo.DlnaOrgOperation.TimeSeekSupported))
                .Append(';');
            _ = sb.Append("DLNA.ORG_CI=")
                .Append(ProtocolInfo.EnumToString(ProtocolInfo.DlnaOrgContentIndex.NoSpecificIndex))
                .Append(';');
            _ = sb.Append("DLNA.ORG_FLAGS=")
                .Append(file.UpnpClass.ToDlnaMedia() == DlnaMedia.Image
                    ? ProtocolInfo.DefaultFlagsInteractive
                    : ProtocolInfo.DefaultFlagsStreaming);
            return sb.ToString();
        }
        private static string? GetResourceThumbnailUrl(ref readonly FileEntity file, string ipEndpoint)
        {
            var sb = new StringBuilder(50);

            if (file.ThumbnailId.HasValue)
            {
                return sb.Append("http://")
                    .Append(ipEndpoint)
                    .Append("/fileserver/thumbnail/")
                    .Append(file.ThumbnailId.ToString())
                    .ToString();
            }
            else
            {
                _ = sb.Append("http://")
                    .Append(ipEndpoint);

                return file.UpnpClass.ToDlnaMedia() switch
                {
                    DlnaMedia.Image => sb.Append("/fileserver/file/").Append(file.Id.ToString()).ToString(),
                    DlnaMedia.Video => sb.Append("/icon/fileMovie.jpg").ToString(),
                    DlnaMedia.Audio => sb.Append("/icon/fileAudio.jpg").ToString(),
                    _ => null,
                };
            }
        }
        private static string GetResourceThumbnailProtocolInfo(ref readonly FileEntity file)
        {
            StringBuilder sb = new();
            _ = sb.Append("http-get:*:")
                .Append(file.Thumbnail?.ThumbnailFileDlnaMime.ToMimeString() ?? "*")
                .Append(':');
            _ = sb.Append("DLNA.ORG_PN=")
                .Append((file.Thumbnail?.ThumbnailFileDlnaMime != null && file.Thumbnail?.ThumbnailFileDlnaMime != DlnaMime.Undefined 
                    ? file.Thumbnail?.ThumbnailFileDlnaProfileName
                    : file.FileDlnaMime.ToDlnaMedia() == DlnaMedia.Image ? file.FileDlnaProfileName
                    : file.FileDlnaMime.ToDlnaMedia() == DlnaMedia.Audio ? DlnaMime.ImageJpeg.ToMainProfileNameString()
                    : file.Thumbnail?.ThumbnailFileExtension?.ToUpper().Replace(".", ""))
                    ?? "")
                .Append(';');
            _ = sb.Append("DLNA.ORG_OP=")
                .Append(ProtocolInfo.FlagsToString(ProtocolInfo.DlnaOrgOperation.None))
                .Append(';');
            _ = sb.Append("DLNA.ORG_CI=")
                .Append(ProtocolInfo.EnumToString(ProtocolInfo.DlnaOrgContentIndex.Thumbnail))
                .Append(';');
            _ = sb.Append("DLNA.ORG_FLAGS=")
                .Append(ProtocolInfo.DefaultFlagsInteractive);
            return sb.ToString();
        }
        private static long GetResourceSize(ref readonly FileEntity file) => file.FileSizeInBytes;
        private static long GetResourceThumbnailSize(ref readonly FileEntity file) => file.Thumbnail?.ThumbnailFileSizeInBytes ?? 0;
        private static string? GetResourceDuration(ref readonly FileEntity file)
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
                var sb = new StringBuilder(8);
                _ = sb.Append((int)(duration.TotalHours))
                    .Append(':')
                    .Append(duration.Minutes.ToString("00"))
                    .Append(':')
                    .Append(duration.Seconds.ToString("00"))
                    .Append('.')
                    .Append(duration.Milliseconds.ToString("000"));
                return sb.ToString();
            }
        }
        private static string? GetResourceResolution(ref readonly FileEntity file)
        {
            switch (file.UpnpClass.ToDlnaMedia())
            {
                case DlnaMedia.Video:
                    {
                        if (file.VideoMetadata is MediaVideoEntity metadata &&
                        metadata.Height.HasValue &&
                            metadata.Width.HasValue)
                        {
                            StringBuilder sb = new();
                            _ = sb.Append(metadata.Width.ToString());
                            _ = sb.Append('x');
                            _ = sb.Append(metadata.Height.ToString());
                            return sb.ToString();
                        }
                    }
                    break;
            }
            return null;
        }
        private static string? GetResourceBitrate(ref readonly FileEntity file)
        {
            switch (file.UpnpClass.ToDlnaMedia())
            {
                case DlnaMedia.Video:
                    {
                        if (file.VideoMetadata is MediaVideoEntity metadata &&
                            metadata.Bitrate.HasValue)
                        {
                            return string.Intern(metadata.Bitrate.Value.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    break;
                case DlnaMedia.Audio:
                    {
                        if (file.AudioMetadata is MediaAudioEntity metadata &&
                            metadata.Bitrate.HasValue)
                        {
                            return string.Intern(metadata.Bitrate.Value.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    break;
            }
            return null;
        }
        private static string? GetVideoCodec(ref readonly FileEntity file)
        {
            switch (file.UpnpClass.ToDlnaMedia())
            {
                case DlnaMedia.Video:
                    {
                        if (file.VideoMetadata is MediaVideoEntity metadata &&
                            !string.IsNullOrWhiteSpace(metadata.Codec))
                        {
                            return string.Intern(metadata.Codec.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    break;
            }
            return null;
        }
        private static string? GetAudioCodec(ref readonly FileEntity file)
        {
            switch (file.UpnpClass.ToDlnaMedia())
            {
                case DlnaMedia.Video:
                    {
                        if (file.AudioMetadata is MediaAudioEntity metadata &&
                            !string.IsNullOrWhiteSpace(metadata.Codec))
                        {
                            return string.Intern(metadata.Codec.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    break;
                case DlnaMedia.Audio:
                    {
                        if (file.AudioMetadata is MediaAudioEntity metadata &&
                            !string.IsNullOrWhiteSpace(metadata.Codec))
                        {
                            return string.Intern(metadata.Codec.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    break;
            }
            return null;
        }
        private static string? GetAudioChannels(ref readonly FileEntity file)
        {
            switch (file.UpnpClass.ToDlnaMedia())
            {
                case DlnaMedia.Video:
                    {
                        if (file.AudioMetadata is MediaAudioEntity metadata &&
                            metadata.Channels.HasValue)
                        {
                            return string.Intern(metadata.Channels.Value.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    break;
                case DlnaMedia.Audio:
                    {
                        if (file.AudioMetadata is MediaAudioEntity metadata &&
                            metadata.Channels.HasValue)
                        {
                            return string.Intern(metadata.Channels.Value.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    break;
            }
            return null;
        }
        private static string? GetTypeOfMedia(ref readonly FileEntity file)
        {
            switch (file.UpnpClass.ToDlnaMedia())
            {
                case DlnaMedia.Video:
                    return string.Intern("video");
                case DlnaMedia.Audio:
                    return string.Intern("audio");
                case DlnaMedia.Image:
                    return string.Intern("image");
                case DlnaMedia.Subtitle:
                    return string.Intern("subtitle");
                default:
                    break;
            }
            return null;
        }

        #endregion
    }
}
