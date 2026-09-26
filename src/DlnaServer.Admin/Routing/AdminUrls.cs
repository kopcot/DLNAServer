using DlnaServer.Core.Dlna;

namespace DlnaServer.Admin.Routing
{
    /// <inheritdoc cref="IAdminUrls"/>
    internal sealed class AdminUrls : IAdminUrls
    {
        public string LibraryRoot => "/admin/library";

        public string Library(Guid directoryPublicId)
        {
            return $"/admin/library/{directoryPublicId}";
        }

        public string Preview(Guid filePublicId)
        {
            return $"/admin/preview/{filePublicId}";
        }

        public string Preview(Guid? directoryPublicId, Guid filePublicId)
        {
            return directoryPublicId is { } id
                ? $"/admin/preview/{id}/{filePublicId}"
                : Preview(filePublicId);
        }

        public string Photo(Guid? directoryPublicId, Guid filePublicId)
        {
            return directoryPublicId is { } id
                ? $"/admin/photo/{id}/{filePublicId}"
                : $"/admin/photo/{filePublicId}";
        }

        public string MediaFile(Guid filePublicId)
        {
            return $"/admin/media/file/{filePublicId}";
        }

        public string Subtitles(Guid filePublicId)
        {
            return $"/admin/media/subtitles/{filePublicId}";
        }

        public string Thumbnail(Guid thumbnailPublicId)
        {
            return $"/admin/media/thumbnail/{thumbnailPublicId}";
        }

        public string? KindIcon(DlnaMedia media)
        {
            var fileName = media switch
            {
                DlnaMedia.Audio => "fileAudio.jpg",
                DlnaMedia.Video => "fileMovie.jpg",
                DlnaMedia.Image => "fileImage.jpg",
                _ => null,
            };

            return fileName is null
                ? null
                : $"/admin/icon/{fileName}";
        }
    }
}
