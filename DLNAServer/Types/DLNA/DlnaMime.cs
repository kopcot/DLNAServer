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
                DlnaMime.Video3gpp => string.Intern("video/3gpp"),
                DlnaMime.VideoAnimaflex => string.Intern("video/animaflex"),
                DlnaMime.VideoAvi => string.Intern("video/avi"),
                DlnaMime.VideoAvsVideo => string.Intern("video/avs-video"),
                DlnaMime.VideoDl => string.Intern("video/dl"),
                DlnaMime.VideoFli => string.Intern("video/fli"),
                DlnaMime.VideoGl => string.Intern("video/gl"),
                DlnaMime.VideoMp2T => string.Intern("video/mp2t"),
                DlnaMime.VideoMp4 => string.Intern("video/mp4"),
                DlnaMime.VideoMpeg => string.Intern("video/mpeg"),
                DlnaMime.VideoMsvideo => string.Intern("video/msvideo"),
                DlnaMime.VideoOgg => string.Intern("video/ogg"),
                DlnaMime.VideoQuicktime => string.Intern("video/quicktime"),
                DlnaMime.VideoVdo => string.Intern("video/vdo"),
                DlnaMime.VideoVivo => string.Intern("video/vivo"),
                DlnaMime.VideoVndRnRealvideo => string.Intern("video/vnd.rn-realvideo"),
                DlnaMime.VideoVndVivo => string.Intern("video/vnd.vivo"),
                DlnaMime.VideoVosaic => string.Intern("video/vosaic"),
                DlnaMime.VideoWebm => string.Intern("video/webm"),
                DlnaMime.VideoXAmtDemorun => string.Intern("video/x-amt-demorun"),
                DlnaMime.VideoXAmtShowrun => string.Intern("video/x-amt-showrun"),
                DlnaMime.VideoXAtomic3DFeature => string.Intern("video/x-atomic3d-feature"),
                DlnaMime.VideoXDl => string.Intern("video/x-dl"),
                DlnaMime.VideoXDv => string.Intern("video/x-dv"),
                DlnaMime.VideoXFli => string.Intern("video/x-fli"),
                DlnaMime.VideoXFlv => string.Intern("video/x-flv"),
                DlnaMime.VideoXGl => string.Intern("video/x-gl"),
                DlnaMime.VideoXIsvideo => string.Intern("video/x-isvideo"),
                DlnaMime.VideoXMatroska => string.Intern("video/x-matroska"),
                DlnaMime.VideoXMotionJpeg => string.Intern("video/x-motion-jpeg"),
                DlnaMime.VideoXMpeg => string.Intern("video/x-mpeg"),
                DlnaMime.VideoXMpeq2A => string.Intern("video/x-mpeq2a"),
                DlnaMime.VideoXMsAsf => string.Intern("video/x-ms-asf"),
                DlnaMime.VideoXMsAsfPlugin => string.Intern("video/x-ms-asf-plugin"),
                DlnaMime.VideoXMsvideo => string.Intern("video/x-msvideo"),
                DlnaMime.VideoXMswmv => string.Intern("video/x-ms-wmv"),
                DlnaMime.VideoXQtc => string.Intern("video/x-qtc"),
                DlnaMime.VideoXScm => string.Intern("video/x-scm"),
                DlnaMime.VideoXSgiMovie => string.Intern("video/x-sgi-movie"),
                DlnaMime.VideoWindowsMetafile => string.Intern("windows/metafile"),
                DlnaMime.VideoXglMovie => string.Intern("xgl/movie"),
                // Audio
                DlnaMime.AudioAac => string.Intern("audio/aac"),
                DlnaMime.AudioAiff => string.Intern("audio/aiff"),
                DlnaMime.AudioBasic => string.Intern("audio/basic"),
                DlnaMime.AudioFlac => string.Intern("audio/flac"),
                DlnaMime.AudioIt => string.Intern("audio/it"),
                DlnaMime.AudioMake => string.Intern("audio/make"),
                DlnaMime.AudioMid => string.Intern("audio/mid"),
                DlnaMime.AudioMidi => string.Intern("audio/midi"),
                DlnaMime.AudioMod => string.Intern("audio/mod"),
                DlnaMime.AudioMpeg => string.Intern("audio/mpeg"),
                DlnaMime.AudioMpeg3 => string.Intern("audio/mpeg3"),
                DlnaMime.AudioNspaudio => string.Intern("audio/nspaudio"),
                DlnaMime.AudioOgg => string.Intern("audio/ogg"),
                DlnaMime.AudioS3M => string.Intern("audio/s3m"),
                DlnaMime.AudioTspAudio => string.Intern("audio/tsp-audio"),
                DlnaMime.AudioTsplayer => string.Intern("audio/tsplayer"),
                DlnaMime.AudioVndQcelp => string.Intern("audio/vnd.qcelp"),
                DlnaMime.AudioVoc => string.Intern("audio/voc"),
                DlnaMime.AudioVoxware => string.Intern("audio/voxware"),
                DlnaMime.AudioWav => string.Intern("audio/wav"),
                DlnaMime.AudioXAdpcm => string.Intern("audio/x-adpcm"),
                DlnaMime.AudioXAiff => string.Intern("audio/x-aiff"),
                DlnaMime.AudioXAu => string.Intern("audio/x-au"),
                DlnaMime.AudioXGsm => string.Intern("audio/x-gsm"),
                DlnaMime.AudioXJam => string.Intern("audio/x-jam"),
                DlnaMime.AudioXLiveaudio => string.Intern("audio/x-liveaudio"),
                DlnaMime.AudioXMatroska => string.Intern("audio/x-matroska"),
                DlnaMime.AudioXMid => string.Intern("audio/x-mid"),
                DlnaMime.AudioXMidi => string.Intern("audio/x-midi"),
                DlnaMime.AudioXMod => string.Intern("audio/x-mod"),
                DlnaMime.AudioXMpeg => string.Intern("audio/x-mpeg"),
                DlnaMime.AudioXMpeg3 => string.Intern("audio/x-mpeg-3"),
                DlnaMime.AudioXMpequrl => string.Intern("audio/x-mpequrl"),
                DlnaMime.AudioXmswma => string.Intern("audio/x-ms-wma"),
                DlnaMime.AudioXNspaudio => string.Intern("audio/x-nspaudio"),
                DlnaMime.AudioXPnRealaudio => string.Intern("audio/x-pn-realaudio"),
                DlnaMime.AudioXPnRealaudioPlugin => string.Intern("audio/x-pn-realaudio-plugin"),
                DlnaMime.AudioXPsid => string.Intern("audio/x-psid"),
                DlnaMime.AudioXRealaudio => string.Intern("audio/x-realaudio"),
                DlnaMime.AudioXTwinvq => string.Intern("audio/x-twinvq"),
                DlnaMime.AudioXTwinvqPlugin => string.Intern("audio/x-twinvq-plugin"),
                DlnaMime.AudioXVndAudioexplosionMjuicemediafile => string.Intern("audio/x-vnd.audioexplosion.mjuicemediafile"),
                DlnaMime.AudioXVoc => string.Intern("audio/x-voc"),
                DlnaMime.AudioXWav => string.Intern("audio/x-wav"),
                DlnaMime.AudioXm => string.Intern("audio/xm"),
                DlnaMime.AudioMusicCrescendo => string.Intern("music/crescendo"),
                DlnaMime.AudioXMusicXMidi => string.Intern("x-music/x-midi"),
                DlnaMime.AudioMp4 => string.Intern("audio/mp4"),
                // Image
                DlnaMime.ImageBmp => string.Intern("image/bmp"),
                DlnaMime.ImageCmuRaster => string.Intern("image/cmu-raster"),
                DlnaMime.ImageFif => string.Intern("image/fif"),
                DlnaMime.ImageFlorian => string.Intern("image/florian"),
                DlnaMime.ImageG3Fax => string.Intern("image/g3fax"),
                DlnaMime.ImageGif => string.Intern("image/gif"),
                DlnaMime.ImageIef => string.Intern("image/ief"),
                DlnaMime.ImageJpeg => string.Intern("image/jpeg"),
                DlnaMime.ImageJutvision => string.Intern("image/jutvision"),
                DlnaMime.ImageNaplps => string.Intern("image/naplps"),
                DlnaMime.ImagePict => string.Intern("image/pict"),
                DlnaMime.ImagePjpeg => string.Intern("image/pjpeg"),
                DlnaMime.ImagePng => string.Intern("image/png"),
                DlnaMime.ImageSvgXml => string.Intern("image/svg+xml"),
                DlnaMime.ImageTiff => string.Intern("image/tiff"),
                DlnaMime.ImageVasa => string.Intern("image/vasa"),
                DlnaMime.ImageVndDwg => string.Intern("image/vnd.dwg"),
                DlnaMime.ImageVndFpx => string.Intern("image/vnd.fpx"),
                DlnaMime.ImageVndRnRealflash => string.Intern("image/vnd.rn-realflash"),
                DlnaMime.ImageVndRnRealpix => string.Intern("image/vnd.rn-realpix"),
                DlnaMime.ImageVndWapWbmp => string.Intern("image/vnd.wap.wbmp"),
                DlnaMime.ImageVndXiff => string.Intern("image/vnd.xiff"),
                DlnaMime.ImageWebp => string.Intern("image/webp"),
                DlnaMime.ImageXCmuRaster => string.Intern("image/x-cmu-raster"),
                DlnaMime.ImageXDwg => string.Intern("image/x-dwg"),
                DlnaMime.ImageXIcon => string.Intern("image/x-icon"),
                DlnaMime.ImageXJg => string.Intern("image/x-jg"),
                DlnaMime.ImageXJps => string.Intern("image/x-jps"),
                DlnaMime.ImageXNiff => string.Intern("image/x-niff"),
                DlnaMime.ImageXPcx => string.Intern("image/x-pcx"),
                DlnaMime.ImageXPict => string.Intern("image/x-pict"),
                DlnaMime.ImageXPortableAnymap => string.Intern("image/x-portable-anymap"),
                DlnaMime.ImageXPortableBitmap => string.Intern("image/x-portable-bitmap"),
                DlnaMime.ImageXPortableGraymap => string.Intern("image/x-portable-graymap"),
                DlnaMime.ImageXPortablePixmap => string.Intern("image/x-portable-pixmap"),
                DlnaMime.ImageXQuicktime => string.Intern("image/x-quicktime"),
                DlnaMime.ImageXRgb => string.Intern("image/x-rgb"),
                DlnaMime.ImageXTiff => string.Intern("image/x-tiff"),
                DlnaMime.ImageXWindowsBmp => string.Intern("image/x-windows-bmp"),
                DlnaMime.ImageXXbitmap => string.Intern("image/x-xbitmap"),
                DlnaMime.ImageXXbm => string.Intern("image/x-xbm"),
                DlnaMime.ImageXXpixmap => string.Intern("image/x-xpixmap"),
                DlnaMime.ImageXXwd => string.Intern("image/x-xwd"),
                DlnaMime.ImageXXwindowdump => string.Intern("image/x-xwindowdump"),
                // Subtitle
                DlnaMime.SubtitleXsubrip => string.Intern("application/x-subrip"),
                DlnaMime.SubtitleVtt => string.Intern("text/vtt"),
                DlnaMime.SubtitleTtmlxml => string.Intern("application/ttml+xml"),
                DlnaMime.SubtitleSubrip => string.Intern("text/srt"),
                DlnaMime.SubtitleMicroDVD => string.Intern("text/vnd.dlna.sub-title"),
                _ => throw new NotImplementedException($"Not defined Mime type (context-type) = {dlnaMime}"),
                //Add new by Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider().TryGetContentType("filemame.extension"), out var mimeType)
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
                DlnaMime.Video3gpp => string.Intern("3GPP video"),
                DlnaMime.VideoAnimaflex => string.Intern("An animation format often used for short clips"),
                DlnaMime.VideoAvi => string.Intern("AVI (Audio Video Interleave) file format"),
                DlnaMime.VideoAvsVideo => string.Intern("AVS video format"),
                DlnaMime.VideoDl => string.Intern("Digital Video (DL) format"),
                DlnaMime.VideoFli => string.Intern("FLI animation format"),
                DlnaMime.VideoGl => string.Intern("GL video format for Silicon Graphics"),
                DlnaMime.VideoMp2T => string.Intern("MPEG-2 Transport Stream"),
                DlnaMime.VideoMp4 => string.Intern("MP4 video file format"),
                DlnaMime.VideoMpeg => string.Intern("MPEG video format"),
                DlnaMime.VideoMsvideo => string.Intern("Microsoft video format"),
                DlnaMime.VideoOgg => string.Intern("Ogg video format"),
                DlnaMime.VideoQuicktime => string.Intern("QuickTime video format"),
                DlnaMime.VideoVdo => string.Intern("VDO format"),
                DlnaMime.VideoVivo => string.Intern("Vivo video format"),
                DlnaMime.VideoVndRnRealvideo => string.Intern("RealVideo format"),
                DlnaMime.VideoVndVivo => string.Intern("Vivo proprietary format"),
                DlnaMime.VideoVosaic => string.Intern("Vosaic video format"),
                DlnaMime.VideoWebm => string.Intern("WebM video format"),
                DlnaMime.VideoXAmtDemorun => string.Intern("Demo run video format"),
                DlnaMime.VideoXAmtShowrun => string.Intern("Show run video format"),
                DlnaMime.VideoXAtomic3DFeature => string.Intern("Atomic 3D feature format"),
                DlnaMime.VideoXDl => string.Intern("Digital Video format"),
                DlnaMime.VideoXDv => string.Intern("DV (Digital Video) format"),
                DlnaMime.VideoXFli => string.Intern("FLI format"),
                DlnaMime.VideoXFlv => string.Intern("Flash Video (FLV)"),
                DlnaMime.VideoXGl => string.Intern("Silicon Graphics GL video format"),
                DlnaMime.VideoXIsvideo => string.Intern("IS video format"),
                DlnaMime.VideoXMatroska => string.Intern("Matroska (MKV) format"),
                DlnaMime.VideoXMotionJpeg => string.Intern("Motion JPEG video"),
                DlnaMime.VideoXMpeg => string.Intern("MPEG video format"),
                DlnaMime.VideoXMpeq2A => string.Intern("MPEG-2 format"),
                DlnaMime.VideoXMsAsf => string.Intern("Microsoft ASF video format"),
                DlnaMime.VideoXMsAsfPlugin => string.Intern("ASF plugin for web browsers"),
                DlnaMime.VideoXMsvideo => string.Intern("Microsoft AVI video format"),
                DlnaMime.VideoXMswmv => string.Intern("Windows Media Video (WMV)"),
                DlnaMime.VideoXQtc => string.Intern("QuickTime Component file"),
                DlnaMime.VideoXScm => string.Intern("SCM video format"),
                DlnaMime.VideoXSgiMovie => string.Intern("SGI Movie format"),
                DlnaMime.VideoWindowsMetafile => string.Intern("Windows Metafile"),
                DlnaMime.VideoXglMovie => string.Intern("XGL (Extensible Graphics Language) movie file"),
                // Audio
                DlnaMime.AudioAac => string.Intern("AAC audio (Advanced Audio Codec)"),
                DlnaMime.AudioAiff => string.Intern("Audio Interchange File Format (AIFF)"),
                DlnaMime.AudioBasic => string.Intern("Basic audio format (Sun/NeXT)"),
                DlnaMime.AudioFlac => string.Intern("Free Lossless Audio Codec"),
                DlnaMime.AudioIt => string.Intern("Impulse Tracker format"),
                DlnaMime.AudioMp4 => string.Intern("MP4 audio (AAC)"),
                DlnaMime.AudioMake => string.Intern("Audio format for MAKE"),
                DlnaMime.AudioMid => string.Intern("MIDI file format"),
                DlnaMime.AudioMidi => string.Intern("Standard MIDI format"),
                DlnaMime.AudioMod => string.Intern("Module format"),
                DlnaMime.AudioMpeg => string.Intern("MPEG audio format"),
                DlnaMime.AudioMpeg3 => string.Intern("MPEG-3 audio format"),
                DlnaMime.AudioNspaudio => string.Intern("NSP Audio"),
                DlnaMime.AudioOgg => string.Intern("Ogg Vorbis audio format"),
                DlnaMime.AudioS3M => string.Intern("Scream Tracker 3 Module"),
                DlnaMime.AudioTspAudio => string.Intern("TSP Audio"),
                DlnaMime.AudioTsplayer => string.Intern("Audio format for TSPlayer"),
                DlnaMime.AudioVndQcelp => string.Intern("Qualcomm PureVoice audio"),
                DlnaMime.AudioVoc => string.Intern("Creative Voice File"),
                DlnaMime.AudioVoxware => string.Intern("Voxware audio format"),
                DlnaMime.AudioWav => string.Intern("Waveform Audio File Format (WAV)"),
                DlnaMime.AudioXAdpcm => string.Intern("ADPCM audio format"),
                DlnaMime.AudioXAiff => string.Intern("Extended AIFF format"),
                DlnaMime.AudioXAu => string.Intern("Extended AU format"),
                DlnaMime.AudioXGsm => string.Intern("GSM audio format"),
                DlnaMime.AudioXJam => string.Intern("JAMP audio format"),
                DlnaMime.AudioXLiveaudio => string.Intern("Live Audio"),
                DlnaMime.AudioXMatroska => string.Intern("Matroska Audio Container"),
                DlnaMime.AudioXMid => string.Intern("Extended MIDI format"),
                DlnaMime.AudioXMidi => string.Intern("Extended MIDI format"),
                DlnaMime.AudioXMod => string.Intern("Extended Module format"),
                DlnaMime.AudioXMpeg => string.Intern("Extended MPEG audio format"),
                DlnaMime.AudioXMpeg3 => string.Intern("Extended MPEG-3 audio format"),
                DlnaMime.AudioXMpequrl => string.Intern("MPEG URL playlist format"),
                DlnaMime.AudioXmswma => string.Intern("Windows Media Audio (WMA)"),
                DlnaMime.AudioXNspaudio => string.Intern("Extended NSP Audio"),
                DlnaMime.AudioXPnRealaudio => string.Intern("RealAudio format"),
                DlnaMime.AudioXPnRealaudioPlugin => string.Intern("RealAudio Plugin format"),
                DlnaMime.AudioXPsid => string.Intern("PSID (Commodore 64 audio)"),
                DlnaMime.AudioXRealaudio => string.Intern("Extended RealAudio format"),
                DlnaMime.AudioXTwinvq => string.Intern("TwinVQ audio compression format"),
                DlnaMime.AudioXTwinvqPlugin => string.Intern("TwinVQ audio plugin format"),
                DlnaMime.AudioXVndAudioexplosionMjuicemediafile => string.Intern("Audio Explosion Media file"),
                DlnaMime.AudioXVoc => string.Intern("Extended Creative Voice File"),
                DlnaMime.AudioXWav => string.Intern("Extended Waveform Audio File Format"),
                DlnaMime.AudioXm => string.Intern("FastTracker II Extended Module Format"),
                DlnaMime.AudioMusicCrescendo => string.Intern("Crescendo MIDI file"),
                DlnaMime.AudioXMusicXMidi => string.Intern("X-MIDI music file format"),
                // Image
                DlnaMime.ImageBmp => string.Intern("Bitmap Image"),
                DlnaMime.ImageCmuRaster => string.Intern("CMU Raster Image"),
                DlnaMime.ImageFif => string.Intern("FIF Image"),
                DlnaMime.ImageFlorian => string.Intern("Florian Image"),
                DlnaMime.ImageG3Fax => string.Intern("G3 Fax Image"),
                DlnaMime.ImageGif => string.Intern("Graphics Interchange Format"),
                DlnaMime.ImageIef => string.Intern("IEF Image"),
                DlnaMime.ImageJpeg => string.Intern("JPEG Image"),
                DlnaMime.ImageJutvision => string.Intern("Jutvision Image"),
                DlnaMime.ImageNaplps => string.Intern("NAPLPS Image"),
                DlnaMime.ImagePict => string.Intern("PICT Image"),
                DlnaMime.ImagePjpeg => string.Intern("Progressive JPEG"),
                DlnaMime.ImagePng => string.Intern("Portable Network Graphics"),
                DlnaMime.ImageSvgXml => string.Intern("Scalable Vector Graphics"),
                DlnaMime.ImageTiff => string.Intern("Tagged Image File Format"),
                DlnaMime.ImageVasa => string.Intern("VASA Image"),
                DlnaMime.ImageVndDwg => string.Intern("AutoCAD Drawing"),
                DlnaMime.ImageVndFpx => string.Intern("FlashPix Image"),
                DlnaMime.ImageVndRnRealflash => string.Intern("RealFlash Image"),
                DlnaMime.ImageVndRnRealpix => string.Intern("RealPix Image"),
                DlnaMime.ImageVndWapWbmp => string.Intern("Wireless Bitmap"),
                DlnaMime.ImageVndXiff => string.Intern("XIFF Image"),
                DlnaMime.ImageWebp => string.Intern("WebP Image"),
                DlnaMime.ImageXCmuRaster => string.Intern("CMU Raster Image"),
                DlnaMime.ImageXDwg => string.Intern("AutoCAD Drawing"),
                DlnaMime.ImageXIcon => string.Intern("Icon Image"),
                DlnaMime.ImageXJg => string.Intern("JG Image"),
                DlnaMime.ImageXJps => string.Intern("JPS Image"),
                DlnaMime.ImageXNiff => string.Intern("NIfTI Image"),
                DlnaMime.ImageXPcx => string.Intern("PCX Image"),
                DlnaMime.ImageXPict => string.Intern("PICT Image"),
                DlnaMime.ImageXPortableAnymap => string.Intern("Portable AnyMap"),
                DlnaMime.ImageXPortableBitmap => string.Intern("Portable Bitmap"),
                DlnaMime.ImageXPortableGraymap => string.Intern("Portable GrayMap"),
                DlnaMime.ImageXPortablePixmap => string.Intern("Portable PixMap"),
                DlnaMime.ImageXQuicktime => string.Intern("QuickTime Image"),
                DlnaMime.ImageXRgb => string.Intern("RGB Image"),
                DlnaMime.ImageXTiff => string.Intern("Tagged Image File Format"),
                DlnaMime.ImageXWindowsBmp => string.Intern("Windows Bitmap"),
                DlnaMime.ImageXXbitmap => string.Intern("XBM Image"),
                DlnaMime.ImageXXbm => string.Intern("XBM Image"),
                DlnaMime.ImageXXpixmap => string.Intern("XPM Image"),
                DlnaMime.ImageXXwd => string.Intern("XWD Image"),
                DlnaMime.ImageXXwindowdump => string.Intern("X Window Dump"),
                // Subtitle
                DlnaMime.SubtitleXsubrip => string.Intern("SubRip subtitle (SRT)"),
                DlnaMime.SubtitleVtt => string.Intern("WebVTT subtitle"),
                DlnaMime.SubtitleTtmlxml => string.Intern("TTML (Timed Text Markup Language) subtitle"),
                DlnaMime.SubtitleSubrip => string.Intern("SubRip subtitle (SRT)"),
                DlnaMime.SubtitleMicroDVD => string.Intern("MicroDVD Subtitle"),
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
                _ => throw new NotImplementedException($"Not defined default file extension for = {dlnaMime}")
            };
        }
        public static string? ToMainProfileNameString(this DlnaMime dlnaMime)
        {
            if (ToProfileNameString(dlnaMime).FirstOrDefault() is string profileName)
            {
                return string.Intern(profileName);
            }

            return null;
        }

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
                _ => throw new NotImplementedException($"Not defined profile name for = {dlnaMime}")
            };
        }
    }
}
