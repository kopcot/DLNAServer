using DlnaServer.Core.Dlna;

namespace DlnaServer.Admin.Routing
{
    /// <summary>
    /// Builds the admin UI's own links, so a route is spelled out in one place.
    /// </summary>
    /// <remarks>
    /// Injected rather than static for the same reason as <see cref="Formatting.IMediaFormatter"/>: nothing
    /// obliges a caller to use a static helper. These four routes were interpolated at a dozen call sites
    /// across five components, and <c>AdminMediaController</c>'s <c>[Route]</c> attribute is one edit away
    /// from breaking every tile and the preview player - with no compile error anywhere, because the links
    /// are strings.
    /// </remarks>
    internal interface IAdminUrls
    {
        /// <summary>
        /// The source-folder listing.
        /// </summary>
        string LibraryRoot { get; }

        /// <summary>
        /// One folder's listing.
        /// </summary>
        string Library(Guid directoryPublicId);

        /// <summary>
        /// One file's preview page, reached from outside any folder.
        /// </summary>
        string Preview(Guid filePublicId);

        /// <summary>
        /// One file's preview page, remembering the folder it was opened from so the page can step to
        /// siblings and offer a way back.
        /// </summary>
        string Preview(Guid? directoryPublicId, Guid filePublicId);

        /// <summary>
        /// One photograph on a page of its own, carrying the folder it was opened from so the way back
        /// leads to the preview it came from.
        /// </summary>
        /// <remarks>
        /// Where a tap on a photograph leads on a phone, where the preview page's zoom overlay is not
        /// offered - a viewport that narrow has nothing to hand the picture that the page itself does not.
        /// </remarks>
        string Photo(Guid? directoryPublicId, Guid filePublicId);

        /// <summary>
        /// The bytes of one media file, served on the admin port.
        /// </summary>
        string MediaFile(Guid filePublicId);

        /// <summary>
        /// The subtitle and lyrics files linked to one media file, as a download - the file itself, or a zip
        /// when there are several.
        /// </summary>
        string Subtitles(Guid filePublicId);

        /// <summary>
        /// One thumbnail image, served on the admin port.
        /// </summary>
        /// <remarks>
        /// Keyed by the thumbnail's own identifier, not the file's: the repository looks these up by
        /// <c>Thumbnails.PublicId</c>, so passing the media file's identifier matches nothing and the image
        /// silently fails to load - which is exactly what happened on the first deployment.
        /// </remarks>
        string Thumbnail(Guid thumbnailPublicId);

        /// <summary>
        /// The stand-in icon for a kind of media, for a file that has no preview of its own. Null for a
        /// kind that has no icon.
        /// </summary>
        /// <remarks>
        /// The same images <c>DidlMapper</c> hands to renderers, served through the admin-prefixed route
        /// because <c>AdminOrMediaPortEndpointFilter</c> answers an unprefixed path on the media port only.
        /// </remarks>
        string? KindIcon(DlnaMedia media);
    }
}
