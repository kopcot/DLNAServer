using DLNAServer.SOAP.Constants;
using System.Xml.Serialization;

namespace DLNAServer.SOAP.Endpoints.Responses.ContentDirectory
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [XmlType(TypeName = "item", Namespace = XmlNamespaces.NS_DIDL)]
    public class BrowseItem
    {
        [XmlAttribute("id")]
        public string ObjectID { get; set; }
        [XmlAttribute("parentID")]
        public string ParentID { get; set; }
        [XmlAttribute("restricted")]
        public string Restricted { get; set; } = "1";
        [XmlAttribute("searchable")]
        public string? Searchable { get; set; }
        [XmlAttribute("childCount")]
        public string? ChildCount { get; set; }
        /// <summary>
        /// <b>res</b><br />
        /// The resource URL for media streaming
        /// </summary>
        [XmlElement(ElementName = "res", Order = 0)]
        public List<Resource> Resource { get; set; }
        /// <summary>
        /// <b>upnp:class</b><br />
        /// The type and format of the media (e.g., audio, video, image)
        /// </summary>
        [XmlElement(ElementName = "class", Namespace = XmlNamespaces.NS_UPNP, Order = 2)]
        public string Class { get; set; }
        /// <summary>
        /// <b>dc:title</b><br />
        /// The title of the media item 
        /// </summary>
        [XmlElement(ElementName = "title", Namespace = XmlNamespaces.NS_DC, Order = 4)]
        public string Title { get; set; }
        /// <summary>
        /// <b>dc:date</b><br />
        /// The date the item was added or created
        /// </summary>
        [XmlElement(ElementName = "date", Namespace = XmlNamespaces.NS_DC, Order = 5)]
        public string Date { get; set; }
        /// <summary>
        /// <b>upnp:comments</b><br />
        /// User comments associated with the media item.
        /// </summary>
        [XmlElement(ElementName = "comments", Namespace = XmlNamespaces.NS_UPNP, Order = 6)]
        public string Comments { get; set; }
        /// <summary>
        /// <b>upnp:genre</b><br />
        /// The genre of the media content
        /// </summary>
        [XmlElement(ElementName = "genre", Namespace = XmlNamespaces.NS_UPNP, Order = 7)]
        public string Genre { get; set; }
        /// <summary>
        /// <b>upnp:videoCodec</b><br />
        /// The codec used for encoding the video (e.g., H.264, HEVC).
        /// </summary>
        [XmlElement(ElementName = "videoCodec", Namespace = XmlNamespaces.NS_UPNP, Order = 9)]
        public string? VideoCodec { get; set; }
        /// <summary>
        /// <b>upnp:audioCodec</b><br />
        /// The codec used for encoding the audio (e.g., AAC, MP3).
        /// </summary>
        [XmlElement(ElementName = "audioCodec", Namespace = XmlNamespaces.NS_UPNP, Order = 10)]
        public string? AudioCodec { get; set; }
        /// <summary>
        /// <b>res</b><br />
        /// The resource URL for media streaming
        /// </summary>
        [XmlElement(ElementName = "res", Order = 100)]
        public Resource? ResourceThumbnail { get; set; }
        /// <summary>
        /// <b>upnp:albumArtURI</b><br />
        /// The URL for the media’s thumbnail image
        /// </summary> 
        [XmlElement(ElementName = "albumArtURI", Namespace = XmlNamespaces.NS_UPNP, Order = 101)]
        public string? ThumbnailUri { get; set; }
        /// <summary>
        /// <b>upnp:icon</b><br />
        /// The URL for the media’s thumbnail image
        /// </summary> 
        [XmlElement(ElementName = "icon", Namespace = XmlNamespaces.NS_UPNP, Order = 102)]
        public string? Icon { get; set; }

    }
    public class Resource
    {
        /// <summary>
        /// <b>res@size</b><br />
        /// The size of the media file in bytes
        /// </summary>
        [XmlAttribute(AttributeName = "size")]
        public long SizeInBytes { get; set; }
        /// <summary>
        /// <b>res@duration</b><br />
        /// The length of the media playback
        /// </summary>
        [XmlAttribute(AttributeName = "duration")]
        public string? Duration { get; set; }
        /// <summary>
        /// <b>res@resolution</b><br />
        /// The resolution of the video or image (e.g., 1920x1080)
        /// </summary>
        [XmlAttribute(AttributeName = "resolution")]
        public string? Resolution { get; set; }
        /// <summary>
        /// <b>res@bitrate</b><br />
        /// The bitrate of the video or audio media
        /// </summary>
        [XmlAttribute(AttributeName = "bitrate")]
        public string? Bitrate { get; set; }
        /// <summary>
        /// <b>res@framerate</b><br />
        /// The frame rate of the video (e.g., 24fps, 30fps)
        /// </summary>
        [XmlAttribute(AttributeName = "framerate")]
        public string? Framerate { get; set; }
        /// <summary>
        /// <b>res@nrAudioChannels</b><br />
        /// The number of audio channels (e.g., stereo, 5.1)
        /// </summary>
        [XmlAttribute(AttributeName = "nrAudioChannels")]
        public string? AudioChannels { get; set; }
        /// <summary>
        /// <b>res@sampleFrequency</b><br />
        /// The sample rate of the audio media (e.g., 44.1kHz)
        /// </summary>
        [XmlAttribute(AttributeName = "sampleFrequency")]
        public string? SampleFrequency { get; set; }
        /// <summary>
        /// <b>res@subtitlesType</b><br />
        /// Specifies the format or delivery method of the subtitles<br /><br />
        /// Subtitle format as <b>"srt", "vtt"</b>, etc., if the format is known and embedded within the video stream or container.<br />
        /// Subtitle delivery format as <b>"external"</b>, if the subtitles are provided as a separate file
        /// </summary>
        [XmlAttribute(AttributeName = "subtitlesType")]
        public string? SubtitlesType { get; set; }
        /// <summary>
        /// <b>res@language</b><br />
        /// The language of the media (audio, text, etc.) 
        /// </summary>
        [XmlAttribute(AttributeName = "language")]
        public string? Language { get; set; }
        /// <summary>
        /// <b>res@protocolInfo</b><br />
        /// Description how media files are shared between devices
        /// </summary>
        [XmlAttribute(AttributeName = "protocolInfo")]
        public string ProtocolInfo { get; set; }
        /// <summary>
        /// <b>res@class</b><br />
        /// The type and format of the media (e.g., audio, video, image)
        /// </summary>
        [XmlAttribute(AttributeName = "class")]
        public string? TypeOfMedia { get; set; }

        [XmlText]
        public string Url { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
//Metadata	                Generic	    Video	Audio	Photo	Other Types	XML Prefix-Name             Description
//Title	                    ✔	        ✔	    ✔	    ✔	    ✔	        dc:title                    The title of the media item   
//Copyright	                ✔	        ✔	    ✔	    ✔	    ✔	        dc:rights                   Copyright information for the media item.
//Original Release Date	    ✔	        ✔	    ✔	    ✔	    ✔	        dc:originalReleaseDate      The original release date of the media item.
//Language Code	            ✔	        ✔	    ✔	    ✔	    ✔	        dc:language                 Language code for the media (e.g., ISO 639-2 language code).   
//Year	                    ✔	        ✔	    ✔	    ✔	    ✔	        dc:date                     The year the media was created or released
//Date Added	            ✔	        ✔	    ✔	    ✔	    ✔	        dc:date                     The date the item was added or created
//Language	                ✔	        ✔	    ✔	    ✘	    ✔	        dc:language                 The language of the media (audio, text, etc.)  
//Artist/Author	            ✔	        ✔	    ✔	    ✔	    ✔	        dc:creator                  The artist or author of the media item           
//File Path/URL	            ✔	        ✔	    ✔	    ✔	    ✔	        res	                        The resource URL for media streaming
//Bitrate	                ✘	        ✔	    ✔	    ✘	    ✘	        res@bitrate	                The bitrate of the video or audio media
//File Format	            ✔	        ✔	    ✔	    ✔	    ✔	        res@class                   The type and format of the media (e.g., audio, video, image)
//Duration	                ✔	        ✔	    ✔	    ✘	    Possibly ✔	res@duration	            The length of the media playback
//Frame Rate	            ✘	        ✔	    ✘	    ✘	    ✘	        res@framerate	            The frame rate of the video (e.g., 24fps, 30fps)
//Audio Channels	        ✘	        ✔	    ✔	    ✘	    ✘	        res@nrAudioChannels	        The number of audio channels (e.g., stereo, 5.1)
//Codec	                    ✘	        ✔	    ✔	    ✘	    ✘	        res@protocolInfo	        The codec used for encoding the media (e.g., H.264, MP3)
//Resolution	            ✘	        ✔	    ✘	    ✔	    ✘	        res@resolution	            The resolution of the video or image (e.g., 1920x1080)
//Sample Rate	            ✘	        ✘	    ✔	    ✘	    ✘	        res@sampleFrequency	        The sample rate of the audio media (e.g., 44.1kHz)
//Subtitles	                ✘	        ✔	    ✘	    ✘	    ✘	        res@subtitles	            The subtitle track URL or file for videos   
//Album Name	            ✘	        ✘	    ✔	    ✘	    ✘	        upnp:album                  The album name of the audio track           
//Thumbnail	                ✔	        ✔	    ✔	    ✔	    ✔	        upnp:albumArtURI            The URL for the media’s thumbnail image
//Album Art	                ✘	        ✘	    ✔	    ✘	    ✘	        upnp:albumArtURI            URL to the album artwork for audio files.       
//Aperture	                ✘	        ✘	    ✘	    ✔	    ✘	        upnp:aperture               The aperture value for the photo   
//File Size	                ✔	        ✔	    ✔	    ✔	    ✔	        upnp:size                   The size of the media file in bytes
//Genre	                    ✔	        ✔	    ✔	    ✘	    ✔	        upnp:genre                  The genre of the media content
//Aspect Ratio	            ✘	        ✔	    ✘	    ✔	    ✘	        upnp:longDescription        The aspect ratio of the video or image (e.g., 16:9)
//Track Number	            ✘	        ✘	    ✔	    ✘	    ✘	        upnp:originalTrackNumber    The track number within an album for audio files
//Camera Model	            ✘	        ✘	    ✘	    ✔	    ✘	        upnp:modelName              The camera model used to take the photo
//Exposure Time	            ✘	        ✘	    ✘	    ✔	    ✘	        upnp:exposureTime           The exposure time for the photo
//ISO Speed	                ✘	        ✘	    ✘	    ✔	    ✘	        upnp:isoSpeed               The ISO speed used for the photo
//GPS Location	            ✘	        ✘	    ✘	    ✔	    ✘	        upnp:location               The GPS coordinates where the photo was taken
//Description               ✔	        ✔	    ✔	    ✔	    ✔	        upnp:description            A short description of the media item.
//Mood	                    ✔	        ✘	    ✔	    ✘	    ✔	        upnp:mood                   The mood or emotion associated with the media.
//Composer	                ✘	        ✘	    ✔	    ✘	    ✘	        upnp:composer               The composer of the audio track (for classical music, etc.).
//Series Title	            ✘	        ✔	    ✘	    ✘	    ✘	        upnp:seriesTitle            The title of the series if the video is part of a series.
//Season Number	            ✘	        ✔	    ✘	    ✘	    ✘	        upnp:seasonNumber           The season number if the video is part of a series.
//Episode Number	        ✘	        ✔	    ✘	    ✘	    ✘	        upnp:episodeNumber          The episode number if the video is part of a series.
//Production Year	        ✔	        ✔	    ✔	    ✔	    ✔	        upnp:productionYear         The year the media was produced or created.
//Country	                ✔	        ✔	    ✔	    ✔	    ✔	        upnp:country                The country where the media was produced.
//Rating	                ✔	        ✔	    ✔	    ✔	    ✔	        upnp:rating                 Rating of the media (e.g., MPAA rating for movies).
//Contributors	            ✔	        ✔	    ✔	    ✔	    ✔	        upnp:contributor            Other contributors associated with the media (e.g., directors, producers).
//Keywords	                ✔	        ✔	    ✔	    ✔	    ✔	        upnp:keywords               Keywords associated with the media item for better searchability.
//Related Items	            ✔	        ✔	    ✔	    ✔	    ✔	        upnp:relatedItems           Links to related media items.
//Custom Properties	        ✔	        ✔	    ✔	    ✔	    ✔	        upnp:customProperty         Any custom properties that are specific to your application.
//Cover Art	                ✘	        ✘	    ✔	    ✘	    ✘	        upnp:coverArt               A URI for the cover art associated with an audio track.
//Video Format	            ✘	        ✔	    ✘	    ✘	    ✘	        upnp:videoFormat            The format of the video (e.g., 4K, HD, SD).
//Audio Format	            ✘	        ✘	    ✔	    ✘	    ✘	        upnp:audioFormat            The format of the audio (e.g., Lossless, AAC, MP3).
//Color Depth	            ✘	        ✘	    ✘	    ✔	    ✘	        upnp:colorDepth             The color depth of the photo (e.g., 24-bit, 32-bit).
//File Size (Compressed)	✔	        ✔	    ✔	    ✔	    ✔	        upnp:compressedSize         The size of the compressed file (if applicable).
//Playback Position	        ✔	        ✔	    ✔	    ✔	    ✔	        upnp:playbackPosition       The current playback position of the media item (e.g., in seconds).
//Last Played Date	        ✔	        ✔	    ✔	    ✔	    ✔	        upnp:lastPlayed             The date the media item was last played.
//Play Count	            ✔	        ✔	    ✔	    ✔	    ✔	        upnp:playCount              The number of times the media item has been played.
//User Rating	            ✔	        ✔	    ✔	    ✔	    ✔	        upnp:userRating             The rating given by users (e.g., out of 5 stars).
//Stream Type	            ✔	        ✔	    ✔	    ✔	    ✔	        upnp:streamType             The type of streaming for the media (e.g., live, on-demand).
//Parental Rating	        ✔	        ✔	    ✔	    ✔	    ✔	        upnp:parentalRating         The parental guidance rating for the media item.
//Recording Date	        ✔	        ✔	    ✘	    ✔	    ✔	        upnp:recordingDate          The date the media was recorded or captured.
//Device Model	            ✔	        ✔	    ✔	    ✔	    ✔	        upnp:deviceModel            The model of the device that created or encoded the media.
//Device Manufacturer	    ✔	        ✔	    ✔	    ✔	    ✔	        upnp:deviceManufacturer     The manufacturer of the device that created or encoded the media.
//Video Quality	            ✘	        ✔	    ✘	    ✘	    ✘	        upnp:videoQuality           The quality setting of the video (e.g., HD, Full HD, 4K).
//Audio Quality	            ✘	        ✘	    ✔	    ✘	    ✘	        upnp:audioQuality           The quality of the audio track (e.g., lossless, compressed).
//Comments	                ✔	        ✔	    ✔	    ✔	    ✔	        upnp:comments               User comments associated with the media item.
//License	                ✔	        ✔	    ✔	    ✔	    ✔	        upnp:license                Licensing information for the media.
//Original Format	        ✔	        ✔	    ✔	    ✔	    ✔	        upnp:originalFormat         The original format of the media before any conversion.
//Video Orientation	        ✘	        ✔	    ✘	    ✔	    ✘	        upnp:orientation            The orientation of the video (e.g., landscape, portrait).
//Color Space	            ✘	        ✔	    ✘	    ✘	    ✘	        upnp:colorSpace             The color space used in the video (e.g., sRGB, Adobe RGB).
//Location Description	    ✘	        ✘	    ✘	    ✔	    ✘	        upnp:locationDescription	A textual description of the location where the photo was taken.
//Featured Artists	        ✘	        ✘	    ✔	    ✘	    ✘	        upnp:featuredArtists	    Artists featured in an audio track.
//Remixers	                ✘	        ✘	    ✔	    ✘	    ✘	        upnp:remixers	            Individuals who have remixed the audio track.
//Instrumental Version	    ✘	        ✘	    ✔	    ✘	    ✘	        upnp:instrumentalVersion	Indicates if the audio track is an instrumental version.
//Video Tagging	            ✔	        ✔	    ✔	    ✔	    ✔	        upnp:videoTagging	        Tags associated with the video content for easier categorization.
//Collection Name	        ✔	        ✔	    ✔	    ✔	    ✔	        upnp:collectionName	        The name of the collection to which the media belongs.
//Bitrate	                ✔	        ✔	    ✔	    ✔	    ✔	        upnp:bitrate	            The bitrate of the media file (e.g., kbps for audio, Mbps for video).
//Sample Rate	            ✘	        ✘	    ✔	    ✘	    ✘	        upnp:sampleRate	            The sample rate of the audio track (e.g., 44.1 kHz, 48 kHz).
//Channel Count	            ✘	        ✘	    ✔	    ✘	    ✘	        upnp:channelCount	        The number of audio channels (e.g., 2 for stereo, 6 for surround sound).
//Aspect Ratio	            ✘	        ✔	    ✘	    ✘	    ✘	        upnp:aspectRatio	        The aspect ratio of the video (e.g., 16:9, 4:3).
//Frame Rate	            ✘	        ✔	    ✘	    ✘	    ✘	        upnp:frameRate	            The frame rate of the video (e.g., 24 fps, 30 fps).
//Color Model	            ✘	        ✔	    ✘	    ✘	    ✘	        upnp:colorModel	            The color model used in the video (e.g., RGB, YUV).
//Photo Dimensions	        ✘	        ✘	    ✘	    ✔	    ✘	        upnp:photoDimensions	    The dimensions of the photo in pixels (e.g., 1920x1080).
//Location Coordinates	    ✔	        ✘	    ✘	    ✔	    ✘	        upnp:locationCoordinates	Geographic coordinates where the photo was taken (latitude, longitude).
//Event Date	            ✔	        ✔	    ✔	    ✔	    ✔	        upnp:eventDate	            The date of the event depicted in the media (e.g., a concert, wedding).
//Duration	                ✔	        ✔	    ✔	    ✔	    ✔	        upnp:duration	            The total duration of the media item (in seconds).
//File Format	            ✔	        ✔	    ✔	    ✔	    ✔	        upnp:fileFormat	            The file format of the media (e.g., MP4, MP3, JPEG).
//Editor	                ✘	        ✘	    ✔	    ✘	    ✘	        upnp:editor	                The person or entity who edited the audio track.
//Production Company	    ✔	        ✔	    ✘	    ✘	    ✔	        upnp:productionCompany	    The company responsible for producing the media item.
//Soundtrack	            ✘	        ✘	    ✔	    ✘	    ✘	        upnp:soundtrack	            The soundtrack associated with the audio or video.
//Subtitles Available	    ✔	        ✔	    ✘	    ✘	    ✔	        upnp:subtitlesAvailable	    Indicates whether subtitles are available for the video.
//Subtitle Language	        ✔	        ✔	    ✘	    ✘	    ✔	        upnp:subtitleLanguage	    The language of the available subtitles.
//Special Features	        ✔	        ✔	    ✔	    ✔	    ✔	        upnp:specialFeatures	    Special features related to the media (e.g., commentary, behind-the-scenes).
//Video Codec	            ✘	        ✔	    ✘	    ✘	    ✘	        upnp:videoCodec	            The codec used for encoding the video (e.g., H.264, HEVC).
//Audio Codec	            ✘	        ✘	    ✔	    ✘	    ✘	        upnp:audioCodec	            The codec used for encoding the audio (e.g., AAC, MP3).
//User Tags	                ✔	        ✔	    ✔	    ✔	    ✔	        upnp:userTags	            Tags created by users for easier organization and discovery.
//Related Media IDs	        ✔	        ✔	    ✔	    ✔	    ✔	        upnp:relatedMediaIDs	    Identifiers of related media items for cross-reference.
//Broadcast Channel	        ✔	        ✔	    ✘	    ✘	    ✔	        upnp:broadcastChannel	    The channel used for broadcasting the video content.
//Original Source	        ✔	        ✔	    ✔	    ✔	    ✔	        upnp:originalSource	        The original source of the media (e.g., VHS, CD, etc.).
//Viewing Instructions	    ✔	        ✔	    ✔	    ✔	    ✔	        upnp:viewingInstructions	Any specific instructions for viewing the media item.