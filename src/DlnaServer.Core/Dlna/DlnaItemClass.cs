namespace DlnaServer.Core.Dlna
{
    /// <summary>
    /// UPnP <c>upnp:class</c> values a DIDL-Lite item or container can carry.
    /// </summary>
    public enum DlnaItemClass
    {
        Unknown = 0,

        Container = 1,
        ContainerAlbum = 2,
        ContainerMusicAlbum = 3,
        ContainerMovie = 4,
        ContainerVideo = 5,
        ContainerPhoto = 6,
        ContainerStorageFolder = 7,

        Generic = 20,

        AudioItem = 40,
        AudioItemMusicTrack = 41,
        AudioItemPodcast = 42,
        AudioItemSoundClip = 43,
        AudioItemSpeech = 44,

        VideoItem = 60,
        VideoItemMovie = 61,
        VideoItemMusicVideoClip = 62,
        VideoItemTvShow = 63,
        VideoItemEpisode = 64,
        VideoItemMovieClip = 65,
        VideoItemAnimation = 66,
        VideoItemTrailer = 67,

        ImageItem = 80,
        ImageItemPhoto = 81,

        TextItem = 100,
    }
}
