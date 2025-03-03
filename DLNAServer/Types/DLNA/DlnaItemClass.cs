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
                DlnaItemClass.Container => string.Intern("object.container"),
                DlnaItemClass.Container_Album => string.Intern("object.container.album"),
                DlnaItemClass.Container_MusicAlbum => string.Intern("object.container.musicAlbum"),
                DlnaItemClass.Container_Movie => string.Intern("object.container.movie"),
                DlnaItemClass.Container_Video => string.Intern("object.container.video"),
                DlnaItemClass.Container_Photo => string.Intern("object.container.photo"),
                DlnaItemClass.Container_StorageFolder => string.Intern("object.container.storageFolder"),
                DlnaItemClass.Generic => string.Intern("object.item"),
                DlnaItemClass.AudioItem => string.Intern("object.item.audioItem"),
                DlnaItemClass.AudioItem_MusicTrack => string.Intern("object.item.audioItem.musicTrack"),
                DlnaItemClass.AudioItem_Podcast => string.Intern("object.item.audioItem.podcast"),
                DlnaItemClass.AudioItem_SoundClip => string.Intern("object.item.audioItem.soundClip"),
                DlnaItemClass.AudioItem_Speech => string.Intern("object.item.audioItem.speech"),
                DlnaItemClass.VideoItem => string.Intern("object.item.videoItem"),
                DlnaItemClass.VideoItem_Movie => string.Intern("object.item.videoItem.movie"),
                DlnaItemClass.VideoItem_MusicVideoClip => string.Intern("object.item.videoItem.musicVideoClip"),
                DlnaItemClass.VideoItem_Trailer => string.Intern("object.item.videoItem.trailer"),
                DlnaItemClass.VideoItem_TvShow => string.Intern("object.item.videoItem.tvShow"),
                DlnaItemClass.VideoItem_Episode => string.Intern("object.item.videoItem.episode"),
                DlnaItemClass.VideoItem_MovieClip => string.Intern("object.item.videoItem.movieClip"),
                DlnaItemClass.VideoItem_Animation => string.Intern("object.item.videoItem.animation"),
                DlnaItemClass.ImageItem => string.Intern("object.item.imageItem"),
                DlnaItemClass.ImageItem_Photo => string.Intern("object.item.imageItem.photo"),
                DlnaItemClass.TextItem => string.Intern("object.item.textItem"),
                _ => throw new NotImplementedException($"Not defined UPNP item class = {dlnaItemClass}"),
            };
        }
        public static string ToDescription(this DlnaItemClass dlnaItemClass)
        {
            return dlnaItemClass switch
            {
                DlnaItemClass.Container => string.Intern("Generic container for organizing items."),
                DlnaItemClass.Container_Album => string.Intern("Container for music albums."),
                DlnaItemClass.Container_MusicAlbum => string.Intern("Container specifically for music albums."),
                DlnaItemClass.Container_Movie => string.Intern("Container for movies."),
                DlnaItemClass.Container_Video => string.Intern("Container for video items."),
                DlnaItemClass.Container_Photo => string.Intern("Container for photo items."),
                DlnaItemClass.Container_StorageFolder => string.Intern("A folder that can contain other items."),
                DlnaItemClass.Generic => string.Intern("Generic media item."),
                DlnaItemClass.AudioItem => string.Intern("Generic audio item."),
                DlnaItemClass.AudioItem_MusicTrack => string.Intern("Individual music track item."),
                DlnaItemClass.AudioItem_SoundClip => string.Intern("Sound clip item."),
                DlnaItemClass.AudioItem_Speech => string.Intern("Speech audio item."),
                DlnaItemClass.AudioItem_Podcast => string.Intern("Podcast audio item."),
                DlnaItemClass.VideoItem => string.Intern("Generic video item."),
                DlnaItemClass.VideoItem_Movie => string.Intern("Movie item."),
                DlnaItemClass.VideoItem_MusicVideoClip => string.Intern("Music video clip item."),
                DlnaItemClass.VideoItem_TvShow => string.Intern("TV show item."),
                DlnaItemClass.VideoItem_Episode => string.Intern("Episode of a TV show."),
                DlnaItemClass.VideoItem_MovieClip => string.Intern("Movie clip item."),
                DlnaItemClass.VideoItem_Animation => string.Intern("Animation item."),
                DlnaItemClass.VideoItem_Trailer => string.Intern("Movie trailer item."),
                DlnaItemClass.ImageItem => string.Intern("Generic image item."),
                DlnaItemClass.ImageItem_Photo => string.Intern("Photo item."),
                DlnaItemClass.TextItem => string.Intern("Text-based item (e.g., eBooks)."),
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
