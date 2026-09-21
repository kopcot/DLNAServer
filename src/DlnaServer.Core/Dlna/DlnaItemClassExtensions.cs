namespace DlnaServer.Core.Dlna
{
    /// <summary>
    /// Maps <see cref="DlnaItemClass"/> to the exact <c>upnp:class</c> strings renderers expect.
    /// </summary>
    public static class DlnaItemClassExtensions
    {
        /// <summary>
        /// Returns the <c>upnp:class</c> string. <see cref="DlnaItemClass.Unknown"/> maps to the generic
        /// <c>object.item</c> rather than throwing - the reference threw here, which turned a stray default
        /// enum value into a failed Browse response for the whole page.
        /// </summary>
        public static string ToUpnpClass(this DlnaItemClass itemClass)
        {
            return itemClass switch
            {
                DlnaItemClass.Container => "object.container",
                DlnaItemClass.ContainerAlbum => "object.container.album",
                DlnaItemClass.ContainerMusicAlbum => "object.container.musicAlbum",
                DlnaItemClass.ContainerMovie => "object.container.movie",
                DlnaItemClass.ContainerVideo => "object.container.video",
                DlnaItemClass.ContainerPhoto => "object.container.photo",
                DlnaItemClass.ContainerStorageFolder => "object.container.storageFolder",
                DlnaItemClass.AudioItem => "object.item.audioItem",
                DlnaItemClass.AudioItemMusicTrack => "object.item.audioItem.musicTrack",
                DlnaItemClass.AudioItemPodcast => "object.item.audioItem.podcast",
                DlnaItemClass.AudioItemSoundClip => "object.item.audioItem.soundClip",
                DlnaItemClass.AudioItemSpeech => "object.item.audioItem.speech",
                DlnaItemClass.VideoItem => "object.item.videoItem",
                DlnaItemClass.VideoItemMovie => "object.item.videoItem.movie",
                DlnaItemClass.VideoItemMusicVideoClip => "object.item.videoItem.musicVideoClip",
                DlnaItemClass.VideoItemTvShow => "object.item.videoItem.tvShow",
                DlnaItemClass.VideoItemEpisode => "object.item.videoItem.episode",
                DlnaItemClass.VideoItemMovieClip => "object.item.videoItem.movieClip",
                DlnaItemClass.VideoItemAnimation => "object.item.videoItem.animation",
                DlnaItemClass.VideoItemTrailer => "object.item.videoItem.trailer",
                DlnaItemClass.ImageItem => "object.item.imageItem",
                DlnaItemClass.ImageItemPhoto => "object.item.imageItem.photo",
                DlnaItemClass.TextItem => "object.item.textItem",
                _ => "object.item",
            };
        }
    }
}
