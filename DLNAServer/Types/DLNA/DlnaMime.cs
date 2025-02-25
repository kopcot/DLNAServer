namespace DLNAServer.Types.DLNA
{
    public enum DlnaMime
    {
        Undefined = 0,

        Video3gpp = 1,                                                          //	3GPP video
        VideoAnimaflex = 2,                                                     //	An animation format often used for short clips
        VideoAvi = 3,                                                           //	AVI (Audio Video Interleave) file format
        VideoAvsVideo = 4,                                                      //	AVS video format
        VideoDl = 5,                                                            //	Digital Video (DL) format
        VideoFli = 6,                                                           //	FLI animation format
        VideoGl = 7,                                                            //	GL video format for Silicon Graphics
        VideoMp2T = 8,                                                          //	MPEG-2 Transport Stream
        VideoMp4 = 9,                                                           //	MP4 video file format
        VideoMpeg = 10,                                                         //	MPEG video format
        VideoMsvideo = 11,                                                      //	Microsoft video format
        VideoOgg = 12,                                                          //	Ogg video format
        VideoQuicktime = 13,                                                    //	QuickTime video format
        VideoVdo = 14,                                                          //	VDO format
        VideoVivo = 15,                                                         //	Vivo video format
        VideoVndRnRealvideo = 16,                                               //	RealVideo format
        VideoVndVivo = 17,                                                      //	Vivo proprietary format
        VideoVosaic = 18,                                                       //	Vosaic video format
        VideoWebm = 19,                                                         //	WebM video format
        VideoXAmtDemorun = 20,                                                  //	Demo run video format
        VideoXAmtShowrun = 21,                                                  //	Show run video format
        VideoXAtomic3DFeature = 22,                                             //	Atomic 3D feature format
        VideoXDl = 23,                                                          //	Digital Video format
        VideoXDv = 24,                                                          //	DV (Digital Video) format
        VideoXFli = 25,                                                         //	FLI format
        VideoXFlv = 26,                                                         //	Flash Video (FLV)
        VideoXGl = 27,                                                          //	Silicon Graphics GL video format
        VideoXIsvideo = 28,                                                     //	IS video format
        VideoXMatroska = 29,                                                    //	Matroska (MKV) format
        VideoXMotionJpeg = 30,                                                  //	Motion JPEG video
        VideoXMpeg = 31,                                                        //	MPEG video format
        VideoXMpeq2A = 32,                                                      //	MPEG-2 format
        VideoXMsAsf = 33,                                                       //	Microsoft ASF video format
        VideoXMsAsfPlugin = 34,                                                 //	ASF plugin for web browsers
        VideoXMsvideo = 35,                                                     //	Microsoft AVI video format
        VideoXMswmv = 36,                                                       //	Windows Media Video (WMV)
        VideoXQtc = 37,                                                         //	QuickTime Component file
        VideoXScm = 38,                                                         //	SCM video format
        VideoXSgiMovie = 39,                                                    //	SGI Movie format
        VideoWindowsMetafile = 40,                                              //	Windows Metafile
        VideoXglMovie = 41,                                                     //	XGL (Extensible Graphics Language) movie file

        AudioAac = 42,                                                          //	AAC audio (Advanced Audio Codec)
        AudioAiff = 43,                                                         //	Audio Interchange File Format (AIFF)
        AudioBasic = 44,                                                        //	Basic audio format (Sun/NeXT)
        AudioFlac = 45,                                                         //	Free Lossless Audio Codec
        AudioIt = 46,                                                           //	Impulse Tracker format
        AudioMake = 47,                                                         //	Audio format for MAKE
        AudioMid = 48,                                                          //	MIDI file format
        AudioMidi = 49,                                                         //	Standard MIDI format
        AudioMod = 50,                                                          //	Module format
        AudioMp4 = 51,                                                          //	MP4 audio (AAC)
        AudioMpeg = 52,                                                         //	MPEG audio format
        AudioMpeg3 = 53,                                                        //	MPEG-3 audio format
        AudioNspaudio = 54,                                                     //	NSP Audio
        AudioOgg = 55,                                                          //	Ogg Vorbis audio format
        AudioS3M = 56,                                                          //	Scream Tracker 3 Module
        AudioTspAudio = 57,                                                     //	TSP Audio
        AudioTsplayer = 58,                                                     //	Audio format for TSPlayer
        AudioVndQcelp = 59,                                                     //	Qualcomm PureVoice audio
        AudioVoc = 60,                                                          //	Creative Voice File
        AudioVoxware = 61,                                                      //	Voxware audio format
        AudioWav = 62,                                                          //	Waveform Audio File Format (WAV)
        AudioXAdpcm = 63,                                                       //	ADPCM audio format
        AudioXAiff = 64,                                                        //	Extended AIFF format
        AudioXAu = 65,                                                          //	Extended AU format
        AudioXGsm = 66,                                                         //	GSM audio format
        AudioXJam = 67,                                                         //	JAMP audio format
        AudioXLiveaudio = 68,                                                   //	Live Audio
        AudioXMatroska = 69,                                                    //	Matroska Audio Container
        AudioXMid = 70,                                                         //	Extended MIDI format
        AudioXMidi = 71,                                                        //	Extended MIDI format
        AudioXMod = 72,                                                         //	Extended Module format
        AudioXMpeg = 73,                                                        //	Extended MPEG audio format
        AudioXMpeg3 = 74,                                                       //	Extended MPEG-3 audio format
        AudioXmswma = 75,                                                       //  Windows Media Audio (WMA)	
        AudioXMpequrl = 76,                                                     //	MPEG URL playlist format
        AudioXNspaudio = 77,                                                    //	Extended NSP Audio
        AudioXPnRealaudio = 78,                                                 //	RealAudio format
        AudioXPnRealaudioPlugin = 79,                                           //	RealAudio Plugin format
        AudioXPsid = 80,                                                        //	PSID (Commodore 64 audio)
        AudioXRealaudio = 81,                                                   //	Extended RealAudio format
        AudioXTwinvq = 82,                                                      //	TwinVQ audio compression format
        AudioXTwinvqPlugin = 83,                                                //	TwinVQ audio plugin format
        AudioXVndAudioexplosionMjuicemediafile = 84,                            //	Audio Explosion Media file
        AudioXVoc = 85,                                                         //	Extended Creative Voice File
        AudioXWav = 86,                                                         //	Extended Waveform Audio File Format
        AudioXm = 87,                                                           //	FastTracker II Extended Module Format
        AudioMusicCrescendo = 88,                                               //	Crescendo MIDI file
        AudioXMusicXMidi = 89,                                                  //	X-MIDI music file format

        ImageBmp = 90,                                                          //	Bitmap Image
        ImageCmuRaster = 91,                                                    //	CMU Raster Image
        ImageFif = 92,                                                          //	FIF Image
        ImageFlorian = 93,                                                      //	Florian Image
        ImageG3Fax = 94,                                                        //	G3 Fax Image
        ImageGif = 95,                                                          //	Graphics Interchange Format
        ImageIef = 96,                                                          //	IEF Image
        ImageJpeg = 97,                                                         //	JPEG Image
        ImageJutvision = 98,                                                    //	Jutvision Image
        ImageNaplps = 99,                                                       //	NAPLPS Image
        ImagePict = 100,                                                        //	PICT Image
        ImagePjpeg = 101,                                                       //	Progressive JPEG
        ImagePng = 102,                                                         //	Portable Network Graphics
        ImageSvgXml = 103,                                                      //	Scalable Vector Graphics
        ImageTiff = 104,                                                        //	Tagged Image File Format
        ImageVasa = 105,                                                        //	VASA Image
        ImageVndDwg = 106,                                                      //	AutoCAD Drawing
        ImageVndFpx = 107,                                                      //	FlashPix Image
        ImageVndRnRealflash = 108,                                              //	RealFlash Image
        ImageVndRnRealpix = 109,                                                //	RealPix Image
        ImageVndWapWbmp = 110,                                                  //	Wireless Bitmap
        ImageVndXiff = 111,                                                     //	XIFF Image
        ImageWebp = 112,                                                        //	WebP Image
        ImageXCmuRaster = 113,                                                  //	CMU Raster Image
        ImageXDwg = 114,                                                        //	AutoCAD Drawing
        ImageXIcon = 115,                                                       //	Icon Image
        ImageXJg = 116,                                                         //	JG Image
        ImageXJps = 117,                                                        //	JPS Image
        ImageXNiff = 118,                                                       //	NIfTI Image
        ImageXPcx = 119,                                                        //	PCX Image
        ImageXPict = 120,                                                       //	PICT Image
        ImageXPortableAnymap = 121,                                             //	Portable AnyMap
        ImageXPortableBitmap = 122,                                             //	Portable Bitmap
        ImageXPortableGraymap = 123,                                            //	Portable GrayMap
        ImageXPortablePixmap = 124,                                             //	Portable PixMap
        ImageXQuicktime = 125,                                                  //	QuickTime Image
        ImageXRgb = 126,                                                        //	RGB Image
        ImageXTiff = 127,                                                       //	Tagged Image File Format
        ImageXWindowsBmp = 128,                                                 //	Windows Bitmap
        ImageXXbitmap = 129,                                                    //	XBM Image
        ImageXXbm = 130,                                                        //	XBM Image
        ImageXXpixmap = 131,                                                    //	XPM Image
        ImageXXwd = 132,                                                        //	XWD Image
        ImageXXwindowdump = 133,                                                //	X Window Dump

        SubtitleXsubrip = 134,                                                  //	SubRip subtitle (SRT)
        SubtitleVtt = 135,                                                      //	WebVTT subtitle
        SubtitleTtmlxml = 136,                                                  //	TTML (Timed Text Markup Language) subtitle
        SubtitleSubrip = 137,                                                   //	SubRip subtitle
        SubtitleMicroDVD = 138,	                                                //	MicroDVD Subtitle)
    }
    public static class DlnaMimeType
    {
        /// <summary>
        /// Return content-type of <see cref="DlnaMedia"/> <br />
        /// For example to <see cref="DlnaMime.VideoMpeg"/> as <b>"video/mpeg"</b>
        /// </summary>
        /// <param name="dlnaMime"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static string ToMimeString(this DlnaMime dlnaMime)
        {
            return dlnaMime switch
            {
                // Video
                DlnaMime.Video3gpp => "video/3gpp",
                DlnaMime.VideoAnimaflex => "video/animaflex",
                DlnaMime.VideoAvi => "video/avi",
                DlnaMime.VideoAvsVideo => "video/avs-video",
                DlnaMime.VideoDl => "video/dl",
                DlnaMime.VideoFli => "video/fli",
                DlnaMime.VideoGl => "video/gl",
                DlnaMime.VideoMp2T => "video/mp2t",
                DlnaMime.VideoMp4 => "video/mp4",
                DlnaMime.VideoMpeg => "video/mpeg",
                DlnaMime.VideoMsvideo => "video/msvideo",
                DlnaMime.VideoOgg => "video/ogg",
                DlnaMime.VideoQuicktime => "video/quicktime",
                DlnaMime.VideoVdo => "video/vdo",
                DlnaMime.VideoVivo => "video/vivo",
                DlnaMime.VideoVndRnRealvideo => "video/vnd.rn-realvideo",
                DlnaMime.VideoVndVivo => "video/vnd.vivo",
                DlnaMime.VideoVosaic => "video/vosaic",
                DlnaMime.VideoWebm => "video/webm",
                DlnaMime.VideoXAmtDemorun => "video/x-amt-demorun",
                DlnaMime.VideoXAmtShowrun => "video/x-amt-showrun",
                DlnaMime.VideoXAtomic3DFeature => "video/x-atomic3d-feature",
                DlnaMime.VideoXDl => "video/x-dl",
                DlnaMime.VideoXDv => "video/x-dv",
                DlnaMime.VideoXFli => "video/x-fli",
                DlnaMime.VideoXFlv => "video/x-flv",
                DlnaMime.VideoXGl => "video/x-gl",
                DlnaMime.VideoXIsvideo => "video/x-isvideo",
                DlnaMime.VideoXMatroska => "video/x-matroska",
                DlnaMime.VideoXMotionJpeg => "video/x-motion-jpeg",
                DlnaMime.VideoXMpeg => "video/x-mpeg",
                DlnaMime.VideoXMpeq2A => "video/x-mpeq2a",
                DlnaMime.VideoXMsAsf => "video/x-ms-asf",
                DlnaMime.VideoXMsAsfPlugin => "video/x-ms-asf-plugin",
                DlnaMime.VideoXMsvideo => "video/x-msvideo",
                DlnaMime.VideoXMswmv => "video/x-ms-wmv",
                DlnaMime.VideoXQtc => "video/x-qtc",
                DlnaMime.VideoXScm => "video/x-scm",
                DlnaMime.VideoXSgiMovie => "video/x-sgi-movie",
                DlnaMime.VideoWindowsMetafile => "windows/metafile",
                DlnaMime.VideoXglMovie => "xgl/movie",
                // Audio
                DlnaMime.AudioAac => "audio/aac",
                DlnaMime.AudioAiff => "audio/aiff",
                DlnaMime.AudioBasic => "audio/basic",
                DlnaMime.AudioFlac => "audio/flac",
                DlnaMime.AudioIt => "audio/it",
                DlnaMime.AudioMake => "audio/make",
                DlnaMime.AudioMid => "audio/mid",
                DlnaMime.AudioMidi => "audio/midi",
                DlnaMime.AudioMod => "audio/mod",
                DlnaMime.AudioMpeg => "audio/mpeg",
                DlnaMime.AudioMpeg3 => "audio/mpeg3",
                DlnaMime.AudioNspaudio => "audio/nspaudio",
                DlnaMime.AudioOgg => "audio/ogg",
                DlnaMime.AudioS3M => "audio/s3m",
                DlnaMime.AudioTspAudio => "audio/tsp-audio",
                DlnaMime.AudioTsplayer => "audio/tsplayer",
                DlnaMime.AudioVndQcelp => "audio/vnd.qcelp",
                DlnaMime.AudioVoc => "audio/voc",
                DlnaMime.AudioVoxware => "audio/voxware",
                DlnaMime.AudioWav => "audio/wav",
                DlnaMime.AudioXAdpcm => "audio/x-adpcm",
                DlnaMime.AudioXAiff => "audio/x-aiff",
                DlnaMime.AudioXAu => "audio/x-au",
                DlnaMime.AudioXGsm => "audio/x-gsm",
                DlnaMime.AudioXJam => "audio/x-jam",
                DlnaMime.AudioXLiveaudio => "audio/x-liveaudio",
                DlnaMime.AudioXMatroska => "audio/x-matroska",
                DlnaMime.AudioXMid => "audio/x-mid",
                DlnaMime.AudioXMidi => "audio/x-midi",
                DlnaMime.AudioXMod => "audio/x-mod",
                DlnaMime.AudioXMpeg => "audio/x-mpeg",
                DlnaMime.AudioXMpeg3 => "audio/x-mpeg-3",
                DlnaMime.AudioXMpequrl => "audio/x-mpequrl",
                DlnaMime.AudioXmswma => "audio/x-ms-wma",
                DlnaMime.AudioXNspaudio => "audio/x-nspaudio",
                DlnaMime.AudioXPnRealaudio => "audio/x-pn-realaudio",
                DlnaMime.AudioXPnRealaudioPlugin => "audio/x-pn-realaudio-plugin",
                DlnaMime.AudioXPsid => "audio/x-psid",
                DlnaMime.AudioXRealaudio => "audio/x-realaudio",
                DlnaMime.AudioXTwinvq => "audio/x-twinvq",
                DlnaMime.AudioXTwinvqPlugin => "audio/x-twinvq-plugin",
                DlnaMime.AudioXVndAudioexplosionMjuicemediafile => "audio/x-vnd.audioexplosion.mjuicemediafile",
                DlnaMime.AudioXVoc => "audio/x-voc",
                DlnaMime.AudioXWav => "audio/x-wav",
                DlnaMime.AudioXm => "audio/xm",
                DlnaMime.AudioMusicCrescendo => "music/crescendo",
                DlnaMime.AudioXMusicXMidi => "x-music/x-midi",
                DlnaMime.AudioMp4 => "audio/mp4",
                // Image
                DlnaMime.ImageBmp => "image/bmp",
                DlnaMime.ImageCmuRaster => "image/cmu-raster",
                DlnaMime.ImageFif => "image/fif",
                DlnaMime.ImageFlorian => "image/florian",
                DlnaMime.ImageG3Fax => "image/g3fax",
                DlnaMime.ImageGif => "image/gif",
                DlnaMime.ImageIef => "image/ief",
                DlnaMime.ImageJpeg => "image/jpeg",
                DlnaMime.ImageJutvision => "image/jutvision",
                DlnaMime.ImageNaplps => "image/naplps",
                DlnaMime.ImagePict => "image/pict",
                DlnaMime.ImagePjpeg => "image/pjpeg",
                DlnaMime.ImagePng => "image/png",
                DlnaMime.ImageSvgXml => "image/svg+xml",
                DlnaMime.ImageTiff => "image/tiff",
                DlnaMime.ImageVasa => "image/vasa",
                DlnaMime.ImageVndDwg => "image/vnd.dwg",
                DlnaMime.ImageVndFpx => "image/vnd.fpx",
                DlnaMime.ImageVndRnRealflash => "image/vnd.rn-realflash",
                DlnaMime.ImageVndRnRealpix => "image/vnd.rn-realpix",
                DlnaMime.ImageVndWapWbmp => "image/vnd.wap.wbmp",
                DlnaMime.ImageVndXiff => "image/vnd.xiff",
                DlnaMime.ImageWebp => "image/webp",
                DlnaMime.ImageXCmuRaster => "image/x-cmu-raster",
                DlnaMime.ImageXDwg => "image/x-dwg",
                DlnaMime.ImageXIcon => "image/x-icon",
                DlnaMime.ImageXJg => "image/x-jg",
                DlnaMime.ImageXJps => "image/x-jps",
                DlnaMime.ImageXNiff => "image/x-niff",
                DlnaMime.ImageXPcx => "image/x-pcx",
                DlnaMime.ImageXPict => "image/x-pict",
                DlnaMime.ImageXPortableAnymap => "image/x-portable-anymap",
                DlnaMime.ImageXPortableBitmap => "image/x-portable-bitmap",
                DlnaMime.ImageXPortableGraymap => "image/x-portable-graymap",
                DlnaMime.ImageXPortablePixmap => "image/x-portable-pixmap",
                DlnaMime.ImageXQuicktime => "image/x-quicktime",
                DlnaMime.ImageXRgb => "image/x-rgb",
                DlnaMime.ImageXTiff => "image/x-tiff",
                DlnaMime.ImageXWindowsBmp => "image/x-windows-bmp",
                DlnaMime.ImageXXbitmap => "image/x-xbitmap",
                DlnaMime.ImageXXbm => "image/x-xbm",
                DlnaMime.ImageXXpixmap => "image/x-xpixmap",
                DlnaMime.ImageXXwd => "image/x-xwd",
                DlnaMime.ImageXXwindowdump => "image/x-xwindowdump",
                // Subtitle
                DlnaMime.SubtitleXsubrip => "application/x-subrip",
                DlnaMime.SubtitleVtt => "text/vtt",
                DlnaMime.SubtitleTtmlxml => "application/ttml+xml",
                DlnaMime.SubtitleSubrip => "text/srt",
                DlnaMime.SubtitleMicroDVD => "text/vnd.dlna.sub-title",
                _ => throw new NotImplementedException($"Not defined Mime type (context-type) = {dlnaMime}"),
                //Add new by Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider().TryGetContentType("filemame.extension", out var mimeType)
            };
        }
        /// <summary>
        /// Return description of <see cref="DlnaMedia"/> <br />
        /// For example to <see cref="DlnaMime.VideoMpeg"/> as <b>"MPEG video format"</b>
        /// </summary>
        public static string ToMimeDescription(this DlnaMime dlnaMime)
        {
            return dlnaMime switch
            {
                // Video
                DlnaMime.Video3gpp => "3GPP video",
                DlnaMime.VideoAnimaflex => "An animation format often used for short clips",
                DlnaMime.VideoAvi => "AVI (Audio Video Interleave) file format",
                DlnaMime.VideoAvsVideo => "AVS video format",
                DlnaMime.VideoDl => "Digital Video (DL) format",
                DlnaMime.VideoFli => "FLI animation format",
                DlnaMime.VideoGl => "GL video format for Silicon Graphics",
                DlnaMime.VideoMp2T => "MPEG-2 Transport Stream",
                DlnaMime.VideoMp4 => "MP4 video file format",
                DlnaMime.VideoMpeg => "MPEG video format",
                DlnaMime.VideoMsvideo => "Microsoft video format",
                DlnaMime.VideoOgg => "Ogg video format",
                DlnaMime.VideoQuicktime => "QuickTime video format",
                DlnaMime.VideoVdo => "VDO format",
                DlnaMime.VideoVivo => "Vivo video format",
                DlnaMime.VideoVndRnRealvideo => "RealVideo format",
                DlnaMime.VideoVndVivo => "Vivo proprietary format",
                DlnaMime.VideoVosaic => "Vosaic video format",
                DlnaMime.VideoWebm => "WebM video format",
                DlnaMime.VideoXAmtDemorun => "Demo run video format",
                DlnaMime.VideoXAmtShowrun => "Show run video format",
                DlnaMime.VideoXAtomic3DFeature => "Atomic 3D feature format",
                DlnaMime.VideoXDl => "Digital Video format",
                DlnaMime.VideoXDv => "DV (Digital Video) format",
                DlnaMime.VideoXFli => "FLI format",
                DlnaMime.VideoXFlv => "Flash Video (FLV)",
                DlnaMime.VideoXGl => "Silicon Graphics GL video format",
                DlnaMime.VideoXIsvideo => "IS video format",
                DlnaMime.VideoXMatroska => "Matroska (MKV) format",
                DlnaMime.VideoXMotionJpeg => "Motion JPEG video",
                DlnaMime.VideoXMpeg => "MPEG video format",
                DlnaMime.VideoXMpeq2A => "MPEG-2 format",
                DlnaMime.VideoXMsAsf => "Microsoft ASF video format",
                DlnaMime.VideoXMsAsfPlugin => "ASF plugin for web browsers",
                DlnaMime.VideoXMsvideo => "Microsoft AVI video format",
                DlnaMime.VideoXMswmv => "Windows Media Video (WMV)",
                DlnaMime.VideoXQtc => "QuickTime Component file",
                DlnaMime.VideoXScm => "SCM video format",
                DlnaMime.VideoXSgiMovie => "SGI Movie format",
                DlnaMime.VideoWindowsMetafile => "Windows Metafile",
                DlnaMime.VideoXglMovie => "XGL (Extensible Graphics Language) movie file",
                // Audio
                DlnaMime.AudioAac => "AAC audio (Advanced Audio Codec)",
                DlnaMime.AudioAiff => "Audio Interchange File Format (AIFF)",
                DlnaMime.AudioBasic => "Basic audio format (Sun/NeXT)",
                DlnaMime.AudioFlac => "Free Lossless Audio Codec",
                DlnaMime.AudioIt => "Impulse Tracker format",
                DlnaMime.AudioMp4 => "MP4 audio (AAC)",
                DlnaMime.AudioMake => "Audio format for MAKE",
                DlnaMime.AudioMid => "MIDI file format",
                DlnaMime.AudioMidi => "Standard MIDI format",
                DlnaMime.AudioMod => "Module format",
                DlnaMime.AudioMpeg => "MPEG audio format",
                DlnaMime.AudioMpeg3 => "MPEG-3 audio format",
                DlnaMime.AudioNspaudio => "NSP Audio",
                DlnaMime.AudioOgg => "Ogg Vorbis audio format",
                DlnaMime.AudioS3M => "Scream Tracker 3 Module",
                DlnaMime.AudioTspAudio => "TSP Audio",
                DlnaMime.AudioTsplayer => "Audio format for TSPlayer",
                DlnaMime.AudioVndQcelp => "Qualcomm PureVoice audio",
                DlnaMime.AudioVoc => "Creative Voice File",
                DlnaMime.AudioVoxware => "Voxware audio format",
                DlnaMime.AudioWav => "Waveform Audio File Format (WAV)",
                DlnaMime.AudioXAdpcm => "ADPCM audio format",
                DlnaMime.AudioXAiff => "Extended AIFF format",
                DlnaMime.AudioXAu => "Extended AU format",
                DlnaMime.AudioXGsm => "GSM audio format",
                DlnaMime.AudioXJam => "JAMP audio format",
                DlnaMime.AudioXLiveaudio => "Live Audio",
                DlnaMime.AudioXMatroska => "Matroska Audio Container",
                DlnaMime.AudioXMid => "Extended MIDI format",
                DlnaMime.AudioXMidi => "Extended MIDI format",
                DlnaMime.AudioXMod => "Extended Module format",
                DlnaMime.AudioXMpeg => "Extended MPEG audio format",
                DlnaMime.AudioXMpeg3 => "Extended MPEG-3 audio format",
                DlnaMime.AudioXMpequrl => "MPEG URL playlist format",
                DlnaMime.AudioXmswma => "Windows Media Audio (WMA)",
                DlnaMime.AudioXNspaudio => "Extended NSP Audio",
                DlnaMime.AudioXPnRealaudio => "RealAudio format",
                DlnaMime.AudioXPnRealaudioPlugin => "RealAudio Plugin format",
                DlnaMime.AudioXPsid => "PSID (Commodore 64 audio)",
                DlnaMime.AudioXRealaudio => "Extended RealAudio format",
                DlnaMime.AudioXTwinvq => "TwinVQ audio compression format",
                DlnaMime.AudioXTwinvqPlugin => "TwinVQ audio plugin format",
                DlnaMime.AudioXVndAudioexplosionMjuicemediafile => "Audio Explosion Media file",
                DlnaMime.AudioXVoc => "Extended Creative Voice File",
                DlnaMime.AudioXWav => "Extended Waveform Audio File Format",
                DlnaMime.AudioXm => "FastTracker II Extended Module Format",
                DlnaMime.AudioMusicCrescendo => "Crescendo MIDI file",
                DlnaMime.AudioXMusicXMidi => "X-MIDI music file format",
                // Image
                DlnaMime.ImageBmp => "Bitmap Image",
                DlnaMime.ImageCmuRaster => "CMU Raster Image",
                DlnaMime.ImageFif => "FIF Image",
                DlnaMime.ImageFlorian => "Florian Image",
                DlnaMime.ImageG3Fax => "G3 Fax Image",
                DlnaMime.ImageGif => "Graphics Interchange Format",
                DlnaMime.ImageIef => "IEF Image",
                DlnaMime.ImageJpeg => "JPEG Image",
                DlnaMime.ImageJutvision => "Jutvision Image",
                DlnaMime.ImageNaplps => "NAPLPS Image",
                DlnaMime.ImagePict => "PICT Image",
                DlnaMime.ImagePjpeg => "Progressive JPEG",
                DlnaMime.ImagePng => "Portable Network Graphics",
                DlnaMime.ImageSvgXml => "Scalable Vector Graphics",
                DlnaMime.ImageTiff => "Tagged Image File Format",
                DlnaMime.ImageVasa => "VASA Image",
                DlnaMime.ImageVndDwg => "AutoCAD Drawing",
                DlnaMime.ImageVndFpx => "FlashPix Image",
                DlnaMime.ImageVndRnRealflash => "RealFlash Image",
                DlnaMime.ImageVndRnRealpix => "RealPix Image",
                DlnaMime.ImageVndWapWbmp => "Wireless Bitmap",
                DlnaMime.ImageVndXiff => "XIFF Image",
                DlnaMime.ImageWebp => "WebP Image",
                DlnaMime.ImageXCmuRaster => "CMU Raster Image",
                DlnaMime.ImageXDwg => "AutoCAD Drawing",
                DlnaMime.ImageXIcon => "Icon Image",
                DlnaMime.ImageXJg => "JG Image",
                DlnaMime.ImageXJps => "JPS Image",
                DlnaMime.ImageXNiff => "NIfTI Image",
                DlnaMime.ImageXPcx => "PCX Image",
                DlnaMime.ImageXPict => "PICT Image",
                DlnaMime.ImageXPortableAnymap => "Portable AnyMap",
                DlnaMime.ImageXPortableBitmap => "Portable Bitmap",
                DlnaMime.ImageXPortableGraymap => "Portable GrayMap",
                DlnaMime.ImageXPortablePixmap => "Portable PixMap",
                DlnaMime.ImageXQuicktime => "QuickTime Image",
                DlnaMime.ImageXRgb => "RGB Image",
                DlnaMime.ImageXTiff => "Tagged Image File Format",
                DlnaMime.ImageXWindowsBmp => "Windows Bitmap",
                DlnaMime.ImageXXbitmap => "XBM Image",
                DlnaMime.ImageXXbm => "XBM Image",
                DlnaMime.ImageXXpixmap => "XPM Image",
                DlnaMime.ImageXXwd => "XWD Image",
                DlnaMime.ImageXXwindowdump => "X Window Dump",
                // Subtitle
                DlnaMime.SubtitleXsubrip => "SubRip subtitle (SRT)",
                DlnaMime.SubtitleVtt => "WebVTT subtitle",
                DlnaMime.SubtitleTtmlxml => "TTML (Timed Text Markup Language) subtitle",
                DlnaMime.SubtitleSubrip => "SubRip subtitle (SRT)",
                DlnaMime.SubtitleMicroDVD => "MicroDVD Subtitle",
                _ => throw new NotImplementedException($"Not defined Mime type description = {dlnaMime}"),
            };
        }
        /// <summary>
        /// Transfer <see cref="DlnaMime"/> to <see cref="DlnaMedia"/> <br />
        /// For example to <see cref="DlnaMedia.Video"/>
        /// </summary>
        public static DlnaMedia ToDlnaMedia(this DlnaMime dlnaMime)
        {
            switch (dlnaMime)
            {
                case DlnaMime.VideoMp4:
                case DlnaMime.VideoMpeg:
                case DlnaMime.VideoXMsvideo:
                case DlnaMime.VideoXMatroska:
                case DlnaMime.VideoXMswmv:
                case DlnaMime.Video3gpp:
                case DlnaMime.VideoQuicktime:
                case DlnaMime.VideoXFlv:
                case DlnaMime.VideoXMsAsf:
                case DlnaMime.VideoOgg:
                case DlnaMime.VideoAnimaflex:
                case DlnaMime.VideoAvi:
                case DlnaMime.VideoAvsVideo:
                case DlnaMime.VideoDl:
                case DlnaMime.VideoFli:
                case DlnaMime.VideoGl:
                case DlnaMime.VideoMp2T:
                case DlnaMime.VideoMsvideo:
                case DlnaMime.VideoVdo:
                case DlnaMime.VideoVivo:
                case DlnaMime.VideoVndRnRealvideo:
                case DlnaMime.VideoVndVivo:
                case DlnaMime.VideoVosaic:
                case DlnaMime.VideoWebm:
                case DlnaMime.VideoXAmtDemorun:
                case DlnaMime.VideoXAmtShowrun:
                case DlnaMime.VideoXAtomic3DFeature:
                case DlnaMime.VideoXDl:
                case DlnaMime.VideoXDv:
                case DlnaMime.VideoXFli:
                case DlnaMime.VideoXGl:
                case DlnaMime.VideoXIsvideo:
                case DlnaMime.VideoXMotionJpeg:
                case DlnaMime.VideoXMpeg:
                case DlnaMime.VideoXMpeq2A:
                case DlnaMime.VideoXMsAsfPlugin:
                case DlnaMime.VideoXQtc:
                case DlnaMime.VideoXScm:
                case DlnaMime.VideoXSgiMovie:
                case DlnaMime.VideoWindowsMetafile:
                case DlnaMime.VideoXglMovie:
                    return DlnaMedia.Video;
                case DlnaMime.AudioMpeg:
                case DlnaMime.AudioMp4:
                case DlnaMime.AudioXmswma:
                case DlnaMime.AudioWav:
                case DlnaMime.AudioXWav:
                case DlnaMime.AudioOgg:
                case DlnaMime.AudioFlac:
                case DlnaMime.AudioAac:
                case DlnaMime.AudioXAiff:
                case DlnaMime.AudioAiff:
                case DlnaMime.AudioBasic:
                case DlnaMime.AudioIt:
                case DlnaMime.AudioMake:
                case DlnaMime.AudioMid:
                case DlnaMime.AudioMidi:
                case DlnaMime.AudioMod:
                case DlnaMime.AudioMpeg3:
                case DlnaMime.AudioNspaudio:
                case DlnaMime.AudioS3M:
                case DlnaMime.AudioTspAudio:
                case DlnaMime.AudioTsplayer:
                case DlnaMime.AudioVndQcelp:
                case DlnaMime.AudioVoc:
                case DlnaMime.AudioVoxware:
                case DlnaMime.AudioXAdpcm:
                case DlnaMime.AudioXAu:
                case DlnaMime.AudioXGsm:
                case DlnaMime.AudioXJam:
                case DlnaMime.AudioXLiveaudio:
                case DlnaMime.AudioXMatroska:
                case DlnaMime.AudioXMid:
                case DlnaMime.AudioXMidi:
                case DlnaMime.AudioXMod:
                case DlnaMime.AudioXMpeg:
                case DlnaMime.AudioXMpeg3:
                case DlnaMime.AudioXMpequrl:
                case DlnaMime.AudioXNspaudio:
                case DlnaMime.AudioXPnRealaudio:
                case DlnaMime.AudioXPnRealaudioPlugin:
                case DlnaMime.AudioXPsid:
                case DlnaMime.AudioXRealaudio:
                case DlnaMime.AudioXTwinvq:
                case DlnaMime.AudioXTwinvqPlugin:
                case DlnaMime.AudioXVndAudioexplosionMjuicemediafile:
                case DlnaMime.AudioXVoc:
                case DlnaMime.AudioXm:
                case DlnaMime.AudioMusicCrescendo:
                case DlnaMime.AudioXMusicXMidi:
                    return DlnaMedia.Audio;
                case DlnaMime.ImageJpeg:
                case DlnaMime.ImagePng:
                case DlnaMime.ImageGif:
                case DlnaMime.ImageBmp:
                case DlnaMime.ImageTiff:
                case DlnaMime.ImageXIcon:
                case DlnaMime.ImageCmuRaster:
                case DlnaMime.ImageFif:
                case DlnaMime.ImageFlorian:
                case DlnaMime.ImageG3Fax:
                case DlnaMime.ImageIef:
                case DlnaMime.ImageJutvision:
                case DlnaMime.ImageNaplps:
                case DlnaMime.ImagePict:
                case DlnaMime.ImagePjpeg:
                case DlnaMime.ImageSvgXml:
                case DlnaMime.ImageVasa:
                case DlnaMime.ImageVndDwg:
                case DlnaMime.ImageVndFpx:
                case DlnaMime.ImageVndRnRealflash:
                case DlnaMime.ImageVndRnRealpix:
                case DlnaMime.ImageVndWapWbmp:
                case DlnaMime.ImageVndXiff:
                case DlnaMime.ImageWebp:
                case DlnaMime.ImageXCmuRaster:
                case DlnaMime.ImageXDwg:
                case DlnaMime.ImageXJg:
                case DlnaMime.ImageXJps:
                case DlnaMime.ImageXNiff:
                case DlnaMime.ImageXPcx:
                case DlnaMime.ImageXPict:
                case DlnaMime.ImageXPortableAnymap:
                case DlnaMime.ImageXPortableBitmap:
                case DlnaMime.ImageXPortableGraymap:
                case DlnaMime.ImageXPortablePixmap:
                case DlnaMime.ImageXQuicktime:
                case DlnaMime.ImageXRgb:
                case DlnaMime.ImageXTiff:
                case DlnaMime.ImageXWindowsBmp:
                case DlnaMime.ImageXXbitmap:
                case DlnaMime.ImageXXbm:
                case DlnaMime.ImageXXpixmap:
                case DlnaMime.ImageXXwd:
                case DlnaMime.ImageXXwindowdump:
                    return DlnaMedia.Image;
                case DlnaMime.SubtitleXsubrip:
                case DlnaMime.SubtitleVtt:
                case DlnaMime.SubtitleTtmlxml:
                case DlnaMime.SubtitleSubrip:
                case DlnaMime.SubtitleMicroDVD:
                    return DlnaMedia.Subtitle;
                default:
                    throw new NotImplementedException($"Not defined Mime media type = {dlnaMime}");
            }
        }
        /// <summary>
        /// Return default <see cref="DlnaItemClass"/> for <see cref="DlnaMime"/> <br />
        /// For example to <see cref="DlnaMime.VideoMpeg"/> as <see cref="DlnaItemClass.VideoItem"/>
        /// </summary>
        public static DlnaItemClass ToDefaultDlnaItemClass(this DlnaMime dlnaMime)
        {
            switch (dlnaMime)
            {
                case DlnaMime.VideoMp4:
                case DlnaMime.VideoMpeg:
                case DlnaMime.VideoXMsvideo:
                case DlnaMime.VideoXMatroska:
                case DlnaMime.VideoXMswmv:
                case DlnaMime.Video3gpp:
                case DlnaMime.VideoQuicktime:
                case DlnaMime.VideoXFlv:
                case DlnaMime.VideoXMsAsf:
                case DlnaMime.VideoOgg:
                case DlnaMime.VideoAnimaflex:
                case DlnaMime.VideoAvi:
                case DlnaMime.VideoAvsVideo:
                case DlnaMime.VideoDl:
                case DlnaMime.VideoFli:
                case DlnaMime.VideoGl:
                case DlnaMime.VideoMp2T:
                case DlnaMime.VideoMsvideo:
                case DlnaMime.VideoVdo:
                case DlnaMime.VideoVivo:
                case DlnaMime.VideoVndRnRealvideo:
                case DlnaMime.VideoVndVivo:
                case DlnaMime.VideoVosaic:
                case DlnaMime.VideoWebm:
                case DlnaMime.VideoXAmtDemorun:
                case DlnaMime.VideoXAmtShowrun:
                case DlnaMime.VideoXAtomic3DFeature:
                case DlnaMime.VideoXDl:
                case DlnaMime.VideoXDv:
                case DlnaMime.VideoXFli:
                case DlnaMime.VideoXGl:
                case DlnaMime.VideoXIsvideo:
                case DlnaMime.VideoXMotionJpeg:
                case DlnaMime.VideoXMpeg:
                case DlnaMime.VideoXMpeq2A:
                case DlnaMime.VideoXMsAsfPlugin:
                case DlnaMime.VideoXQtc:
                case DlnaMime.VideoXScm:
                case DlnaMime.VideoXSgiMovie:
                case DlnaMime.VideoWindowsMetafile:
                case DlnaMime.VideoXglMovie:
                    return DlnaItemClass.VideoItem;
                case DlnaMime.AudioMpeg:
                case DlnaMime.AudioMp4:
                case DlnaMime.AudioXmswma:
                case DlnaMime.AudioWav:
                case DlnaMime.AudioXWav:
                case DlnaMime.AudioOgg:
                case DlnaMime.AudioFlac:
                case DlnaMime.AudioAac:
                case DlnaMime.AudioXAiff:
                case DlnaMime.AudioAiff:
                case DlnaMime.AudioBasic:
                case DlnaMime.AudioIt:
                case DlnaMime.AudioMake:
                case DlnaMime.AudioMid:
                case DlnaMime.AudioMidi:
                case DlnaMime.AudioMod:
                case DlnaMime.AudioMpeg3:
                case DlnaMime.AudioNspaudio:
                case DlnaMime.AudioS3M:
                case DlnaMime.AudioTspAudio:
                case DlnaMime.AudioTsplayer:
                case DlnaMime.AudioVndQcelp:
                case DlnaMime.AudioVoc:
                case DlnaMime.AudioVoxware:
                case DlnaMime.AudioXAdpcm:
                case DlnaMime.AudioXAu:
                case DlnaMime.AudioXGsm:
                case DlnaMime.AudioXJam:
                case DlnaMime.AudioXLiveaudio:
                case DlnaMime.AudioXMatroska:
                case DlnaMime.AudioXMid:
                case DlnaMime.AudioXMidi:
                case DlnaMime.AudioXMod:
                case DlnaMime.AudioXMpeg:
                case DlnaMime.AudioXMpeg3:
                case DlnaMime.AudioXMpequrl:
                case DlnaMime.AudioXNspaudio:
                case DlnaMime.AudioXPnRealaudio:
                case DlnaMime.AudioXPnRealaudioPlugin:
                case DlnaMime.AudioXPsid:
                case DlnaMime.AudioXRealaudio:
                case DlnaMime.AudioXTwinvq:
                case DlnaMime.AudioXTwinvqPlugin:
                case DlnaMime.AudioXVndAudioexplosionMjuicemediafile:
                case DlnaMime.AudioXVoc:
                case DlnaMime.AudioXm:
                case DlnaMime.AudioMusicCrescendo:
                case DlnaMime.AudioXMusicXMidi:
                    return DlnaItemClass.AudioItem;
                case DlnaMime.ImageJpeg:
                case DlnaMime.ImagePng:
                case DlnaMime.ImageGif:
                case DlnaMime.ImageBmp:
                case DlnaMime.ImageTiff:
                case DlnaMime.ImageXIcon:
                case DlnaMime.ImageCmuRaster:
                case DlnaMime.ImageFif:
                case DlnaMime.ImageFlorian:
                case DlnaMime.ImageG3Fax:
                case DlnaMime.ImageIef:
                case DlnaMime.ImageJutvision:
                case DlnaMime.ImageNaplps:
                case DlnaMime.ImagePict:
                case DlnaMime.ImagePjpeg:
                case DlnaMime.ImageSvgXml:
                case DlnaMime.ImageVasa:
                case DlnaMime.ImageVndDwg:
                case DlnaMime.ImageVndFpx:
                case DlnaMime.ImageVndRnRealflash:
                case DlnaMime.ImageVndRnRealpix:
                case DlnaMime.ImageVndWapWbmp:
                case DlnaMime.ImageVndXiff:
                case DlnaMime.ImageWebp:
                case DlnaMime.ImageXCmuRaster:
                case DlnaMime.ImageXDwg:
                case DlnaMime.ImageXJg:
                case DlnaMime.ImageXJps:
                case DlnaMime.ImageXNiff:
                case DlnaMime.ImageXPcx:
                case DlnaMime.ImageXPict:
                case DlnaMime.ImageXPortableAnymap:
                case DlnaMime.ImageXPortableBitmap:
                case DlnaMime.ImageXPortableGraymap:
                case DlnaMime.ImageXPortablePixmap:
                case DlnaMime.ImageXQuicktime:
                case DlnaMime.ImageXRgb:
                case DlnaMime.ImageXTiff:
                case DlnaMime.ImageXWindowsBmp:
                case DlnaMime.ImageXXbitmap:
                case DlnaMime.ImageXXbm:
                case DlnaMime.ImageXXpixmap:
                case DlnaMime.ImageXXwd:
                case DlnaMime.ImageXXwindowdump:
                    return DlnaItemClass.ImageItem;
                case DlnaMime.SubtitleXsubrip:
                case DlnaMime.SubtitleVtt:
                case DlnaMime.SubtitleTtmlxml:
                case DlnaMime.SubtitleSubrip:
                case DlnaMime.SubtitleMicroDVD:
                case DlnaMime.Undefined:
                default:
                    return DlnaItemClass.Generic;
            }
        }
        /// <summary>
        /// Return default list of extensions for <see cref="DlnaMime"/> <br /> 
        /// </summary>
        public static string[] DefaultFileExtensions(this DlnaMime dlnaMime)
        {
            return dlnaMime switch
            {
                // Video
                DlnaMime.VideoAnimaflex => [".afl"],
                DlnaMime.VideoAvi => [".avi"],
                DlnaMime.VideoAvsVideo => [".avs"],
                DlnaMime.VideoDl => [".dl"],
                DlnaMime.VideoFli => [".fli"],
                DlnaMime.VideoGl => [".gl"],
                DlnaMime.VideoMp2T => [".ts"],
                DlnaMime.VideoMp4 => [".mp4", ".3gp"],
                DlnaMime.VideoMpeg => [".m1v", ".m2v", ".mp2", ".mp3", ".mpa", ".mpe", ".mpeg", ".mpg"],
                DlnaMime.VideoMsvideo => [".avi"],
                DlnaMime.VideoOgg => [".ogg", ".ogv"],
                DlnaMime.VideoQuicktime => [".moov", ".mov", ".qt"],
                DlnaMime.VideoVdo => [".vdo"],
                DlnaMime.VideoVivo => [".viv", ".vivo"],
                DlnaMime.VideoVndRnRealvideo => [".rv",],
                DlnaMime.VideoVndVivo => [".viv", ".vivo"],
                DlnaMime.VideoVosaic => [".vos"],
                DlnaMime.VideoWebm => [".webm"],
                DlnaMime.VideoXAmtDemorun => [".xdr"],
                DlnaMime.VideoXAmtShowrun => [".xsr"],
                DlnaMime.VideoXAtomic3DFeature => [".fmf"],
                DlnaMime.VideoXDl => [".dl"],
                DlnaMime.VideoXDv => [".dif", ".dv"],
                DlnaMime.VideoXFli => [".fli"],
                DlnaMime.VideoXGl => [".gl"],
                DlnaMime.VideoXIsvideo => [".isu"],
                DlnaMime.VideoXMatroska => [".mkv"],
                DlnaMime.VideoXMotionJpeg => [".mjpg"],
                DlnaMime.VideoXMpeg => [".mp2", ".mp3"],
                DlnaMime.VideoXMpeq2A => [".mp2"],
                DlnaMime.VideoXMsAsf => [".asf", ".asx", ".asx"],
                DlnaMime.VideoXMsAsfPlugin => [".asx"],
                DlnaMime.VideoXMsvideo => [".avi"],
                DlnaMime.VideoXQtc => [".qtc"],
                DlnaMime.VideoXScm => [".scm"],
                DlnaMime.VideoXSgiMovie => [".movie", ".mv"],
                DlnaMime.VideoWindowsMetafile => [".wmf"],
                DlnaMime.VideoXglMovie => [".xmz"],
                // Audio
                DlnaMime.AudioAiff => [".aif", ".aifc", ".aiff"],
                DlnaMime.AudioBasic => [".au", ".snd"],
                DlnaMime.AudioFlac => [".flac"],
                DlnaMime.AudioIt => [".it"],
                DlnaMime.AudioMake => [".funk", ".my", ".pfunk"],
                DlnaMime.AudioMid => [".rmi"],
                DlnaMime.AudioMidi => [".kar", ".mid", ".midi"],
                DlnaMime.AudioMod => [".mod"],
                DlnaMime.AudioMpeg => [".m2a", ".mp2", ".mpa", ".mpg", ".mpga"],
                DlnaMime.AudioMpeg3 => [".mp3"],
                DlnaMime.AudioNspaudio => [".la", ".lma"],
                DlnaMime.AudioOgg => [".ogg", ".oga"],
                DlnaMime.AudioS3M => [".s3m"],
                DlnaMime.AudioTspAudio => [".tsi"],
                DlnaMime.AudioTsplayer => [".tsp"],
                DlnaMime.AudioVndQcelp => [".qcp"],
                DlnaMime.AudioVoc => [".voc"],
                DlnaMime.AudioVoxware => [".vox"],
                DlnaMime.AudioWav => [".wav"],
                DlnaMime.AudioXAdpcm => [".snd"],
                DlnaMime.AudioXAiff => [".aif", ".aifc", ".aiff"],
                DlnaMime.AudioXAu => [".au"],
                DlnaMime.AudioXGsm => [".gsd", ".gsm"],
                DlnaMime.AudioXJam => [".jam"],
                DlnaMime.AudioXLiveaudio => [".lam"],
                DlnaMime.AudioXMatroska => [".mka"],
                DlnaMime.AudioXMid => [".mid", ".midi"],
                DlnaMime.AudioXMidi => [".mid", ".midi"],
                DlnaMime.AudioXMod => [".mod"],
                DlnaMime.AudioXMpeg => [".mp2"],
                DlnaMime.AudioXMpeg3 => [".mp3"],
                DlnaMime.AudioXMpequrl => [".m3u"],
                DlnaMime.AudioXNspaudio => [".la", ".lma"],
                DlnaMime.AudioXPnRealaudio => [".ra", ".ram", ".rm", ".rmm", ".rmp"],
                DlnaMime.AudioXPnRealaudioPlugin => [".ra", ".rmp", ".rpm"],
                DlnaMime.AudioXPsid => [".sid"],
                DlnaMime.AudioXRealaudio => [".ra"],
                DlnaMime.AudioXTwinvq => [".vqf"],
                DlnaMime.AudioXTwinvqPlugin => [".vqe", ".vql"],
                DlnaMime.AudioXVndAudioexplosionMjuicemediafile => [".mjf"],
                DlnaMime.AudioXVoc => [".voc"],
                DlnaMime.AudioXWav => [".wav"],
                DlnaMime.AudioXm => [".xm"],
                DlnaMime.AudioMusicCrescendo => [".mid", ".midi"],
                DlnaMime.AudioXMusicXMidi => [".mid", ".midi"],
                // Image
                DlnaMime.ImageBmp => [".bm", ".bmp"],
                DlnaMime.ImageCmuRaster => [".ras", ".rast"],
                DlnaMime.ImageFif => [".fif"],
                DlnaMime.ImageFlorian => [".flo", ".turbot"],
                DlnaMime.ImageG3Fax => [".g3"],
                DlnaMime.ImageGif => [".gif"],
                DlnaMime.ImageIef => [".ief", ".iefs"],
                DlnaMime.ImageJpeg => [".jpg", ".jfif", ".jfif-tbnl", ".jpe", ".jpeg"],
                DlnaMime.ImageJutvision => [".jut"],
                DlnaMime.ImageNaplps => [".nap", ".naplps"],
                DlnaMime.ImagePict => [".pic", ".pict"],
                DlnaMime.ImagePjpeg => [".jfif", ".jpe", ".jpeg", ".jpg"],
                DlnaMime.ImagePng => [".png", ".x-png"],
                DlnaMime.ImageSvgXml => [".svg"],
                DlnaMime.ImageTiff => [".tif", ".tiff"],
                DlnaMime.ImageVasa => [".mcf"],
                DlnaMime.ImageVndDwg => [".dwg", ".dxf", ".svf"],
                DlnaMime.ImageVndFpx => [".fpx", ".vnd.net-fpx"],
                DlnaMime.ImageVndRnRealflash => [".rf"],
                DlnaMime.ImageVndRnRealpix => [".rp"],
                DlnaMime.ImageVndWapWbmp => [".wbmp"],
                DlnaMime.ImageVndXiff => [".xif"],
                DlnaMime.ImageWebp => [".webp"],
                DlnaMime.ImageXCmuRaster => [".ras"],
                DlnaMime.ImageXDwg => [".dwg", ".dxf", ".svf"],
                DlnaMime.ImageXIcon => [".ico"],
                DlnaMime.ImageXJg => [".art"],
                DlnaMime.ImageXJps => [".jps"],
                DlnaMime.ImageXNiff => [".nif", ".niff"],
                DlnaMime.ImageXPcx => [".pcx"],
                DlnaMime.ImageXPict => [".pct", ".pict"],
                DlnaMime.ImageXPortableAnymap => [".pnm"],
                DlnaMime.ImageXPortableBitmap => [".pbm"],
                DlnaMime.ImageXPortableGraymap => [".pgm"],
                DlnaMime.ImageXPortablePixmap => [".ppm"],
                DlnaMime.ImageXQuicktime => [".qif", ".qti", ".qtif"],
                DlnaMime.ImageXRgb => [".rgb"],
                DlnaMime.ImageXTiff => [".tif", ".tiff"],
                DlnaMime.ImageXWindowsBmp => [".bmp"],
                DlnaMime.ImageXXbitmap => [".xbm"],
                DlnaMime.ImageXXbm => [".xbm"],
                DlnaMime.ImageXXpixmap => [".pm", ".xpm"],
                DlnaMime.ImageXXwd => [".xwd"],
                DlnaMime.ImageXXwindowdump => [".xwd"],
                DlnaMime.Video3gpp => [".3gp"],
                DlnaMime.VideoXFlv => [".flv"],
                DlnaMime.VideoXMswmv => [".wmv"],
                DlnaMime.AudioAac => [".aac"],
                DlnaMime.AudioMp4 => [".m4a", ".m4p", ".m4b", ".m4r"],
                DlnaMime.AudioXmswma => [".wma"],
                DlnaMime.SubtitleXsubrip => [".srt"],
                DlnaMime.SubtitleVtt => [".vtt"],
                DlnaMime.SubtitleTtmlxml => [".ttml"],
                DlnaMime.SubtitleSubrip => [".srt"],
                DlnaMime.SubtitleMicroDVD => [".sub"],
                _ => throw new NotImplementedException($"Not defined default file extension for = {dlnaMime}"),
            };
        }
        public static string? ToMainProfileNameString(this DlnaMime dlnaMime)
            => ToProfileNameString(dlnaMime).FirstOrDefault();
        /// <summary>
        /// Return list of DLNA profile names of <see cref="DlnaMedia"/> <br /> 
        /// </summary>
        public static string[] ToProfileNameString(this DlnaMime dlnaMime)
        {
            return dlnaMime switch
            {
                // Audio
                DlnaMime.AudioAac => [
                    "AAC",
                    ],
                DlnaMime.AudioAiff => [
                    "AIF",
                    ],
                DlnaMime.AudioBasic => [
                    "SND",
                    ],
                DlnaMime.AudioFlac => [
                    "FLAC",
                    ],
                DlnaMime.AudioIt => [
                    "IT",
                    ],
                DlnaMime.AudioMake => [
                    "MAKE",
                    ],
                DlnaMime.AudioMid => [
                    "MIDI",
                    ],
                DlnaMime.AudioMidi => [
                    "MIDI",
                    ],
                DlnaMime.AudioMod => [
                    "MOD",
                    ],
                DlnaMime.AudioMpeg => [
                    "MPEG",
                    "MP2_MPS",
                    ],
                DlnaMime.AudioMpeg3 => [
                    "MP3",
                    ],
                DlnaMime.AudioNspaudio => [
                    "NSP",
                    ],
                DlnaMime.AudioOgg => [
                    "OGG",
                    ],
                DlnaMime.AudioS3M => [
                    "S3M",
                    ],
                DlnaMime.AudioTspAudio => [
                    "TSPA",
                    ],
                DlnaMime.AudioTsplayer => [
                    "TSPL",
                    ],
                DlnaMime.AudioVndQcelp => [
                    "QCELP",
                    ],
                DlnaMime.AudioVoc => [
                    "VOC",
                    ],
                DlnaMime.AudioVoxware => [
                    "VOX",
                    ],
                DlnaMime.AudioWav => [
                    "WAVE",
                    ],
                DlnaMime.AudioXAdpcm => [
                    "XADPCM",
                    ],
                DlnaMime.AudioXAiff => [
                    "XAIF",
                    ],
                DlnaMime.AudioXAu => [
                    "XAU",
                    ],
                DlnaMime.AudioXGsm => [
                    "XGSM",
                    ],
                DlnaMime.AudioXJam => [
                    "XJAM",
                    ],
                DlnaMime.AudioXLiveaudio => [
                    "XLA",
                    ],
                DlnaMime.AudioXMatroska => [
                    "XMKV",
                    ],
                DlnaMime.AudioXMid => [
                    "XMID",
                    ],
                DlnaMime.AudioXMidi => [
                    "XMIDI",
                    ],
                DlnaMime.AudioXMod => [
                    "XMOD",
                    ],
                DlnaMime.AudioXMpeg => [
                    "XMPEG",
                    ],
                DlnaMime.AudioXMpeg3 => [
                    "XMP3",
                    ],
                DlnaMime.AudioXMpequrl => [
                    "XMPQ",
                    ],
                DlnaMime.AudioXmswma => [
                    "XWMA",
                    ],
                DlnaMime.AudioXNspaudio => [
                    "XNSPA",
                    ],
                DlnaMime.AudioXPnRealaudio => [
                    "XREAL",
                    ],
                DlnaMime.AudioXPnRealaudioPlugin => [
                    "XREALP",
                    ],
                DlnaMime.AudioXPsid => [
                    "XPSID",
                    ],
                DlnaMime.AudioXRealaudio => [
                    "XREALA",
                    ],
                DlnaMime.AudioXTwinvq => [
                    "XTWINVQ",
                    ],
                DlnaMime.AudioXTwinvqPlugin => [
                    "XTWINVQP",
                    ],
                DlnaMime.AudioXVndAudioexplosionMjuicemediafile => [
                    "XVND",
                    ],
                DlnaMime.AudioXVoc => [
                    "XVOC",
                    ],
                DlnaMime.AudioXWav => [
                    "XWAVE",
                    ],
                DlnaMime.AudioXm => [
                    "XM",
                    ],
                DlnaMime.AudioMusicCrescendo => [
                    "CRESCENDO",
                    ],
                DlnaMime.AudioXMusicXMidi => [
                    "XMIDI",
                    ],
                DlnaMime.AudioMp4 => [
                    "MP4",
                    ],
                // Video
                DlnaMime.Video3gpp => [
                    "AVC_MP4_BL_CIF15_AAC_520",
                    "MPEG4_P2_3GPP_SP_L0B_AMR",
                    "AVC_3GPP_BL_QCIF15_AAC",
                    "MPEG4_H263_3GPP_P0_L10_AMR",
                    "MPEG4_H263_MP4_P0_L10_AAC",
                    "MPEG4_P2_3GPP_SP_L0B_AAC",
                    ],
                DlnaMime.VideoAnimaflex => [],
                DlnaMime.VideoAvi => [
                    "AVI",
                    "AVI_HD",
                    ],
                DlnaMime.VideoAvsVideo => [],
                DlnaMime.VideoDl => [],
                DlnaMime.VideoFli => [],
                DlnaMime.VideoGl => [],
                DlnaMime.VideoMp2T => [
                    "MPEG_TS_SD_EU",
                    "MPEG_TS_SD_NA",
                    "MPEG_TS_HD_NA",
                    "MPEG_TS_HD_EU",
                    ],
                DlnaMime.VideoMp4 => [
                    "AVC_MP4_BL_CIF15_AAC_520",
                    "AVC_MP4_MP_SD_AAC_MULT5",
                    "AVC_MP4_HP_HD_AAC",
                    "AVC_MP4_HP_HD_DTS",
                    "AVC_MP4_LPCM",
                    "AVC_MP4_MP_SD_AC3",
                    "AVC_MP4_MP_SD_DTS",
                    "AVC_MP4_MP_SD_MPEG1_L3",
                    "AVC_TS_HD_50_LPCM_T",
                    "AVC_TS_HD_DTS_ISO",
                    "AVC_TS_HD_DTS_T",
                    "AVC_TS_HP_HD_MPEG1_L2_ISO",
                    "AVC_TS_HP_HD_MPEG1_L2_T",
                    "AVC_TS_HP_SD_MPEG1_L2_ISO",
                    "AVC_TS_HP_SD_MPEG1_L2_T",
                    "AVC_TS_MP_HD_AAC_MULT5",
                    "AVC_TS_MP_HD_AAC_MULT5_ISO",
                    "AVC_TS_MP_HD_AAC_MULT5_T",
                    "AVC_TS_MP_HD_AC3",
                    "AVC_TS_MP_HD_AC3_ISO",
                    "AVC_TS_MP_HD_AC3_T",
                    "AVC_TS_MP_HD_MPEG1_L3",
                    "AVC_TS_MP_HD_MPEG1_L3_ISO",
                    "AVC_TS_MP_HD_MPEG1_L3_T",
                    "AVC_TS_MP_SD_AAC_MULT5",
                    "AVC_TS_MP_SD_AAC_MULT5_ISO",
                    "AVC_TS_MP_SD_AAC_MULT5_T",
                    "AVC_TS_MP_SD_AC3",
                    "AVC_TS_MP_SD_AC3_ISO",
                    "AVC_TS_MP_SD_AC3_T",
                    "AVC_TS_MP_SD_MPEG1_L3",
                    "AVC_TS_MP_SD_MPEG1_L3_ISO",
                    "AVC_TS_MP_SD_MPEG1_L3_T",
                    ],
                DlnaMime.VideoMpeg => [
                    "MPEG1",
                    "MPEG_PS_PAL",
                    "MPEG_PS_NTSC",
                    "MPEG_TS_SD_EU",
                    "MPEG_TS_SD_EU_T",
                    "MPEG_TS_SD_EU_ISO",
                    "MPEG_TS_SD_NA",
                    "MPEG_TS_SD_NA_T",
                    "MPEG_TS_SD_NA_ISO",
                    "MPEG_TS_SD_KO",
                    "MPEG_TS_SD_KO_T",
                    "MPEG_TS_SD_KO_ISO",
                    "MPEG_TS_JP_T",
                    ],
                DlnaMime.VideoMsvideo => [],
                DlnaMime.VideoOgg => [],
                DlnaMime.VideoQuicktime => [
                    "QT",
                    "QT_HD",
                    ],
                DlnaMime.VideoVdo => [],
                DlnaMime.VideoVivo => [],
                DlnaMime.VideoVndRnRealvideo => [],
                DlnaMime.VideoVndVivo => [],
                DlnaMime.VideoVosaic => [],
                DlnaMime.VideoWebm => [
                    "WEBM",
                    ],
                DlnaMime.VideoXAmtDemorun => [],
                DlnaMime.VideoXAmtShowrun => [],
                DlnaMime.VideoXAtomic3DFeature => [],
                DlnaMime.VideoXDl => [],
                DlnaMime.VideoXDv => [
                    "DVR_MS_VIDEO",
                    ],
                DlnaMime.VideoXFli => [],
                DlnaMime.VideoXFlv => [
                    "FLV",
                    ],
                DlnaMime.VideoXGl => [],
                DlnaMime.VideoXIsvideo => [],
                DlnaMime.VideoXMatroska => [
                    "MATROSKA",
                    ],
                DlnaMime.VideoXMotionJpeg => [
                    "MJPEG",
                    ],
                DlnaMime.VideoXMpeg => [
                    "MPEG_PS_PAL",
                    "MPEG_PS_NTSC",
                    ],
                DlnaMime.VideoXMpeq2A => [],
                DlnaMime.VideoXMsAsf => [
                    "WMV",
                    "WMV_HD",
                    ],
                DlnaMime.VideoXMsAsfPlugin => [],
                DlnaMime.VideoXMsvideo => [
                    "MSVIDEO",
                    ],
                DlnaMime.VideoXMswmv => [
                    "WMV",
                    "WMV_HD",
                    "WMV_FULL",
                    "WMV_BASE",
                    "WMVHIGH_FULL",
                    "WMVHIGH_BASE",
                    "WMVHIGH_PRO",
                    "WMVMED_FULL",
                    "WMVMED_BASE",
                    "WMVMED_PRO",
                    "VC1_ASF_AP_L1_WMA",
                    "VC1_ASF_AP_L2_WMA",
                    "VC1_ASF_AP_L3_WMA",
                    ],
                DlnaMime.VideoXQtc => [],
                DlnaMime.VideoXScm => [],
                DlnaMime.VideoXSgiMovie => [],
                DlnaMime.VideoWindowsMetafile => [],
                DlnaMime.VideoXglMovie => [],
                // Image
                DlnaMime.ImageBmp => [
                    "BMP",
                    ],
                DlnaMime.ImageCmuRaster => [
                    "CMURASTER",
                    ],
                DlnaMime.ImageFif => [
                    "FIF",
                    ],
                DlnaMime.ImageFlorian => [
                    "FLORIAN",
                    ],
                DlnaMime.ImageG3Fax => [
                    "G3FAX",
                    ],
                DlnaMime.ImageGif => [
                    "GIF",
                    "GIF_LRG",
                    "GIF_MED",
                    "GIF_SM",
                    ],
                DlnaMime.ImageIef => ["IEF",
                    ],
                DlnaMime.ImageJpeg => [
                    "JPEG",
                    "JPEG_LRG",
                    "JPEG_MED",
                    "JPEG_SM",
                    "JPEG_TN",
                    ],
                DlnaMime.ImageJutvision => [
                    "JUTVISION",
                    ],
                DlnaMime.ImageNaplps => [
                    "NAPLPS",
                    ],
                DlnaMime.ImagePict => [
                    "PICT",
                    ],
                DlnaMime.ImagePjpeg => [
                    "PJPEG",
                    ],
                DlnaMime.ImagePng => [
                    "PNG",
                    "PNG_LRG",
                    "PNG_MED",
                    "PNG_SM",
                    "PNG_TN",
                    ],
                DlnaMime.ImageSvgXml => [
                    "SVGXML",
                    ],
                DlnaMime.ImageTiff => [
                    "TIFF",
                ],
                DlnaMime.ImageVasa => [
                    "VASA",
                ],
                DlnaMime.ImageVndDwg => [
                    "VNDDWG",
                    ],
                DlnaMime.ImageVndFpx => [
                    "VNDFPX",
                    ],
                DlnaMime.ImageVndRnRealflash => [
                    "VNDRNREALFLASH",
                    ],
                DlnaMime.ImageVndRnRealpix => [
                    "VNDRNREALPIX",
                    ],
                DlnaMime.ImageVndWapWbmp => [
                    "VNDWAPWBMP",
                    ],
                DlnaMime.ImageVndXiff => [
                    "VNDXIFF",
                    ],
                DlnaMime.ImageWebp => [
                    "WEBP",
                    ],
                DlnaMime.ImageXCmuRaster => [
                    "XCMURASTER",
                    ],
                DlnaMime.ImageXDwg => [
                    "XDWG",
                    ],
                DlnaMime.ImageXIcon => [
                    "XICON",
                    ],
                DlnaMime.ImageXJg => [
                    "XJG",
                    ],
                DlnaMime.ImageXJps => [
                    "XJPS",
                    ],
                DlnaMime.ImageXNiff => [
                    "XNIFF",
                    ],
                DlnaMime.ImageXPcx => [
                    "XPCX",
                    ],
                DlnaMime.ImageXPict => [
                    "XPICT",
                    ],
                DlnaMime.ImageXPortableAnymap => [
                    "XPORTABLEANYMAP",
                    ],
                DlnaMime.ImageXPortableBitmap => [
                    "XPORTABLEBITMAP",
                    ],
                DlnaMime.ImageXPortableGraymap => [
                    "XPORTABLEGRAYMAP",
                    ],
                DlnaMime.ImageXPortablePixmap => [
                    "XPORTABLEPIXMAP",
                    ],
                DlnaMime.ImageXQuicktime => [
                    "XQUICKTIME",
                    ],
                DlnaMime.ImageXRgb => [
                    "XRGB",
                    ],
                DlnaMime.ImageXTiff => [
                    "XTIFF",
                    ],
                DlnaMime.ImageXWindowsBmp => [
                    "XWINDOWSBMP",
                    ],
                DlnaMime.ImageXXbitmap => [
                    "XXBITMAP",
                    ],
                DlnaMime.ImageXXbm => [
                    "XXBM",
                    ],
                DlnaMime.ImageXXpixmap => [
                    "XXPIXMAP",
                    ],
                DlnaMime.ImageXXwd => [
                    "XXWD",
                    ],
                DlnaMime.ImageXXwindowdump => [
                    "XXWINDOWDUMP",
                    ],
                DlnaMime.SubtitleXsubrip => [
                    "SRT",
                    ],
                DlnaMime.SubtitleVtt => [],
                DlnaMime.SubtitleTtmlxml => [],
                DlnaMime.SubtitleMicroDVD => [],
                DlnaMime.SubtitleSubrip => [],
                _ => throw new NotImplementedException($"Not defined profile name for = {dlnaMime}"),
            };
        }
    }
}
