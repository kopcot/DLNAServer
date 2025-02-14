namespace DLNAServer.Types.DLNA
{
    public enum DlnaItemClass
    {
        Container,
        Container_Album,
        Container_MusicAlbum,
        Container_Movie,
        Container_Video,
        Container_Photo,
        Container_StorageFolder,
        Generic,
        AudioItem,
        AudioItem_MusicTrack,
        AudioItem_Podcast,
        AudioItem_SoundClip,
        AudioItem_Speech,
        VideoItem,
        VideoItem_Movie,
        VideoItem_MusicVideoClip,
        VideoItem_TvShow,
        VideoItem_Episode,
        VideoItem_MovieClip,
        VideoItem_Animation,
        VideoItem_Trailer,
        ImageItem,
        ImageItem_Photo,
        TextItem,
    }
    public static class DlnaItemClassType
    {
        public static string ToItemClass(this DlnaItemClass dlnaItemClass)
        {
            return dlnaItemClass switch
            {
                DlnaItemClass.Container => "object.container",
                DlnaItemClass.Container_Album => "object.container.album",
                DlnaItemClass.Container_MusicAlbum => "object.container.musicAlbum",
                DlnaItemClass.Container_Movie => "object.container.movie",
                DlnaItemClass.Container_Video => "object.container.video",
                DlnaItemClass.Container_Photo => "object.container.photo",
                DlnaItemClass.Container_StorageFolder => "object.container.storageFolder",
                DlnaItemClass.Generic => "object.item",
                DlnaItemClass.AudioItem => "object.item.audioItem",
                DlnaItemClass.AudioItem_MusicTrack => "object.item.audioItem.musicTrack",
                DlnaItemClass.AudioItem_Podcast => "object.item.audioItem.podcast",
                DlnaItemClass.AudioItem_SoundClip => "object.item.audioItem.soundClip",
                DlnaItemClass.AudioItem_Speech => "object.item.audioItem.speech",
                DlnaItemClass.VideoItem => "object.item.videoItem",
                DlnaItemClass.VideoItem_Movie => "object.item.videoItem.movie",
                DlnaItemClass.VideoItem_MusicVideoClip => "object.item.videoItem.musicVideoClip",
                DlnaItemClass.VideoItem_Trailer => "object.item.videoItem.trailer",
                DlnaItemClass.VideoItem_TvShow => "object.item.videoItem.tvShow",
                DlnaItemClass.VideoItem_Episode => "object.item.videoItem.episode",
                DlnaItemClass.VideoItem_MovieClip => "object.item.videoItem.movieClip",
                DlnaItemClass.VideoItem_Animation => "object.item.videoItem.animation",
                DlnaItemClass.ImageItem => "object.item.imageItem",
                DlnaItemClass.ImageItem_Photo => "object.item.imageItem.photo",
                DlnaItemClass.TextItem => "object.item.textItem",
                _ => throw new NotImplementedException($"Not defined UPNP item class = {dlnaItemClass}"),
            };
        }
        public static string ToDescription(this DlnaItemClass dlnaItemClass)
        {
            return dlnaItemClass switch
            {
                DlnaItemClass.Container => "Generic container for organizing items.",
                DlnaItemClass.Container_Album => "Container for music albums.",
                DlnaItemClass.Container_MusicAlbum => "Container specifically for music albums.",
                DlnaItemClass.Container_Movie => "Container for movies.",
                DlnaItemClass.Container_Video => "Container for video items.",
                DlnaItemClass.Container_Photo => "Container for photo items.",
                DlnaItemClass.Container_StorageFolder => "A folder that can contain other items.",
                DlnaItemClass.Generic => "Generic media item.",
                DlnaItemClass.AudioItem => "Generic audio item.",
                DlnaItemClass.AudioItem_MusicTrack => "Individual music track item.",
                DlnaItemClass.AudioItem_SoundClip => "Sound clip item.",
                DlnaItemClass.AudioItem_Speech => "Speech audio item.",
                DlnaItemClass.AudioItem_Podcast => "Podcast audio item.",
                DlnaItemClass.VideoItem => "Generic video item.",
                DlnaItemClass.VideoItem_Movie => "Movie item.",
                DlnaItemClass.VideoItem_MusicVideoClip => "Music video clip item.",
                DlnaItemClass.VideoItem_TvShow => "TV show item.",
                DlnaItemClass.VideoItem_Episode => "Episode of a TV show.",
                DlnaItemClass.VideoItem_MovieClip => "Movie clip item.",
                DlnaItemClass.VideoItem_Animation => "Animation item.",
                DlnaItemClass.VideoItem_Trailer => "Movie trailer item.",
                DlnaItemClass.ImageItem => "Generic image item.",
                DlnaItemClass.ImageItem_Photo => "Photo item.",
                DlnaItemClass.TextItem => "Text-based item (e.g., eBooks).",
                _ => throw new NotImplementedException($"Not defined item class description = '{dlnaItemClass}'"),
            };
        }
        public static DlnaMedia ToDlnaMedia(this DlnaItemClass dlnaMime)
        {
            switch (dlnaMime)
            {
                case DlnaItemClass.AudioItem:
                case DlnaItemClass.AudioItem_MusicTrack:
                case DlnaItemClass.AudioItem_Podcast:
                case DlnaItemClass.AudioItem_SoundClip:
                case DlnaItemClass.AudioItem_Speech:
                    return DlnaMedia.Audio;

                case DlnaItemClass.VideoItem:
                case DlnaItemClass.VideoItem_Movie:
                case DlnaItemClass.VideoItem_MusicVideoClip:
                case DlnaItemClass.VideoItem_TvShow:
                case DlnaItemClass.VideoItem_Episode:
                case DlnaItemClass.VideoItem_MovieClip:
                case DlnaItemClass.VideoItem_Animation:
                case DlnaItemClass.VideoItem_Trailer:
                    return DlnaMedia.Video;

                case DlnaItemClass.ImageItem:
                case DlnaItemClass.ImageItem_Photo:
                    return DlnaMedia.Image;

                case DlnaItemClass.Container:
                case DlnaItemClass.Container_Album:
                case DlnaItemClass.Container_MusicAlbum:
                case DlnaItemClass.Container_Movie:
                case DlnaItemClass.Container_Video:
                case DlnaItemClass.Container_Photo:
                case DlnaItemClass.Container_StorageFolder:
                    return DlnaMedia.Container;

                case DlnaItemClass.Generic:
                case DlnaItemClass.TextItem:
                default:
                    return DlnaMedia.Unknown;
            }
        }
    }
}
