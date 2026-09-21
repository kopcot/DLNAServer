using System.Collections.Frozen;

namespace DlnaServer.Core.Dlna
{
    /// <summary>
    /// Single source of truth for MIME strings, DLNA profile names, media kinds and file extensions.
    /// </summary>
    /// <remarks>
    /// One table rather than the reference's five parallel <c>switch</c> statements. Those could disagree -
    /// and did: a member present in one switch but missing from another threw at runtime mid-response.
    /// Every value below is carried over verbatim from the reference so what reaches a renderer is unchanged.
    /// </remarks>
    public static class DlnaMimeCatalog
    {
        private static readonly FrozenDictionary<DlnaMime, DlnaMimeInfo> _byMime = CreateTable();
        private static readonly FrozenDictionary<string, DlnaMime> _byExtension = CreateExtensionIndex(_byMime);

        /// <summary>
        /// All known MIME descriptors, ordered by enum value.
        /// </summary>
        public static IReadOnlyCollection<DlnaMimeInfo> All { get; } =
            _byMime.Values.OrderBy(static i => i.Mime).ToArray();

        public static bool TryGetInfo(DlnaMime mime, out DlnaMimeInfo info)
        {
            return _byMime.TryGetValue(mime, out info!);
        }

        /// <summary>
        /// The <c>Content-Type</c> string for <paramref name="mime"/>, or <c>application/octet-stream</c>
        /// when it is unknown - an unrecognised file is still served, just without a specific type.
        /// </summary>
        public static string ToMimeString(this DlnaMime mime)
        {
            return _byMime.TryGetValue(mime, out var info)
                ? info.MimeString
                : "application/octet-stream";
        }

        public static DlnaMedia ToMedia(this DlnaMime mime)
        {
            return _byMime.TryGetValue(mime, out var info)
                ? info.Media
                : DlnaMedia.Unknown;
        }

        /// <summary>
        /// The DLNA.ORG_PN profile advertised for this MIME, or null when it has none.
        /// </summary>
        public static string? ToMainProfileName(this DlnaMime mime)
        {
            return _byMime.TryGetValue(mime, out var info)
                ? info.MainProfileName
                : null;
        }

        public static IReadOnlyList<string> ToProfileNames(this DlnaMime mime)
        {
            return _byMime.TryGetValue(mime, out var info)
                ? info.ProfileNames
                : [];
        }

        public static IReadOnlyList<string> ToFileExtensions(this DlnaMime mime)
        {
            return _byMime.TryGetValue(mime, out var info)
                ? info.FileExtensions
                : [];
        }

        /// <summary>
        /// Default <c>upnp:class</c> for a file of this MIME. Indexing assigns the coarse class;
        /// richer subclasses are a deliberate later decision, never inferred here.
        /// </summary>
        public static DlnaItemClass ToDefaultItemClass(this DlnaMime mime)
        {
            return mime.ToMedia() switch
            {
                DlnaMedia.Video => DlnaItemClass.VideoItem,
                DlnaMedia.Audio => DlnaItemClass.AudioItem,
                DlnaMedia.Image => DlnaItemClass.ImageItem,
                _ => DlnaItemClass.Generic,
            };
        }

        /// <summary>
        /// Whether a television can be offered content of this MIME's kind as a playable item.
        /// </summary>
        /// <remarks>
        /// Video, audio and images. This is what limits the extension fallback below to kinds that mean
        /// something in a listing: <see cref="DlnaMedia.Subtitle"/> would otherwise put every
        /// <c>.srt</c> and <c>.vtt</c> beside the films as a <see cref="DlnaItemClass.Generic"/> item,
        /// and <see cref="DlnaMedia.Unknown"/> and <see cref="DlnaMedia.Container"/> are not files a
        /// renderer can be handed at all.
        /// <para>
        /// Read by scanning, to decide what the fallback may infer, and by ConnectionManager's
        /// <c>GetProtocolInfo</c>, to decide what this server claims it can serve. One definition,
        /// because those two disagreeing means serving a MIME that was never advertised.
        /// </para>
        /// </remarks>
        public static bool IsPresentableMedia(this DlnaMime mime)
        {
            return mime.ToMedia() is DlnaMedia.Video or DlnaMedia.Audio or DlnaMedia.Image;
        }

        /// <summary>
        /// Whether this server both advertises this MIME and can present it - the set a file may be
        /// given, and the set <c>GetProtocolInfo</c> lists in <c>SourceProtocolInfo</c>.
        /// </summary>
        /// <remarks>
        /// <see cref="IsPresentableMedia"/> is only half the test. A MIME the catalogue knows but maps to
        /// no file extension is never advertised, so a file carrying one claims a type the server said it
        /// could not serve - and a renderer that pre-filters on <c>SourceProtocolInfo</c> drops it.
        /// <para>
        /// One definition, read by ConnectionManager to decide what to advertise and by the admin editor
        /// to decide what may be chosen. The two disagreeing is the failure this exists to prevent.
        /// </para>
        /// </remarks>
        public static bool IsServable(this DlnaMime mime)
        {
            return mime.IsPresentableMedia()
                && _byMime.TryGetValue(mime, out var info)
                && info.FileExtensions.Count > 0;
        }

        /// <summary>
        /// Every servable MIME, ordered by the string an operator reads rather than by enum value.
        /// </summary>
        /// <remarks>
        /// Sorted once, at type initialisation. The admin UI offers this list from two pages, and sorting
        /// ~120 entries inside a render body re-ran it on every keystroke and every re-render.
        /// <para>
        /// Deliberately separate from <see cref="All"/>, which keeps enum order because the protocol code
        /// reads it and that order is part of what goes on the wire.
        /// </para>
        /// </remarks>
        public static IReadOnlyList<DlnaMimeInfo> Servable { get; } =
            [.. All.Where(static i => i.Mime.IsServable())
                .OrderBy(static i => i.Mime.ToMimeString(), StringComparer.Ordinal)];

        /// <summary>
        /// Resolves a file extension to its conventional MIME. This is only a fallback -
        /// a configured extension mapping wins, because that is where device quirks live.
        /// </summary>
        /// <remarks>
        /// The fallback tier <c>LibraryScanner.TryResolveMime</c> reaches when configuration names no
        /// mapping for an extension. It answers for every kind, subtitles included, so a caller deciding
        /// what to index filters with <see cref="IsPresentableMedia"/> - the raw index stays honest about
        /// what the catalog knows, which is what <c>DlnaMimeCatalogGrowthTest</c> guards.
        /// </remarks>
        public static bool TryGetByFileExtension(string fileExtension, out DlnaMime mime)
        {
            ArgumentNullException.ThrowIfNull(fileExtension);

            return _byExtension.TryGetValue(fileExtension, out mime);
        }

        private static FrozenDictionary<string, DlnaMime> CreateExtensionIndex(
            FrozenDictionary<DlnaMime, DlnaMimeInfo> table)
        {
            var index = new Dictionary<string, DlnaMime>(StringComparer.OrdinalIgnoreCase);

            foreach (var info in table.Values.OrderBy(static i => i.Mime))
            {
                foreach (var extension in info.FileExtensions)
                {
                    // First writer wins, so the preferred MIME for a shared extension is the lowest enum value.
                    _ = index.TryAdd(extension, info.Mime);
                }
            }

            return index.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        private static FrozenDictionary<DlnaMime, DlnaMimeInfo> CreateTable()
        {
            DlnaMimeInfo[] entries =
            [
                // Video
                Video(DlnaMime.VideoMp4, "video/mp4", [".mp4", ".m4v"],
                    "AVC_MP4_BL_CIF15_AAC_520", "AVC_MP4_MP_SD_AAC_MULT5", "AVC_MP4_HP_HD_AAC", "AVC_MP4_HP_HD_DTS",
                    "AVC_MP4_LPCM", "AVC_MP4_MP_SD_AC3", "AVC_MP4_MP_SD_DTS", "AVC_MP4_MP_SD_MPEG1_L3",
                    "AVC_TS_HD_50_LPCM_T", "AVC_TS_HD_DTS_ISO", "AVC_TS_HD_DTS_T", "AVC_TS_HP_HD_MPEG1_L2_ISO",
                    "AVC_TS_HP_HD_MPEG1_L2_T", "AVC_TS_HP_SD_MPEG1_L2_ISO", "AVC_TS_HP_SD_MPEG1_L2_T",
                    "AVC_TS_MP_HD_AAC_MULT5", "AVC_TS_MP_HD_AAC_MULT5_ISO", "AVC_TS_MP_HD_AAC_MULT5_T",
                    "AVC_TS_MP_HD_AC3", "AVC_TS_MP_HD_AC3_ISO", "AVC_TS_MP_HD_AC3_T", "AVC_TS_MP_HD_MPEG1_L3",
                    "AVC_TS_MP_HD_MPEG1_L3_ISO", "AVC_TS_MP_HD_MPEG1_L3_T", "AVC_TS_MP_SD_AAC_MULT5",
                    "AVC_TS_MP_SD_AAC_MULT5_ISO", "AVC_TS_MP_SD_AAC_MULT5_T", "AVC_TS_MP_SD_AC3",
                    "AVC_TS_MP_SD_AC3_ISO", "AVC_TS_MP_SD_AC3_T", "AVC_TS_MP_SD_MPEG1_L3",
                    "AVC_TS_MP_SD_MPEG1_L3_ISO", "AVC_TS_MP_SD_MPEG1_L3_T"),
                Video(DlnaMime.VideoMpeg, "video/mpeg", [".mpeg", ".mpg", ".mpe", ".m1v", ".m2v"],
                    "MPEG1", "MPEG_PS_PAL", "MPEG_PS_NTSC", "MPEG_TS_SD_EU", "MPEG_TS_SD_EU_T", "MPEG_TS_SD_EU_ISO",
                    "MPEG_TS_SD_NA", "MPEG_TS_SD_NA_T", "MPEG_TS_SD_NA_ISO", "MPEG_TS_SD_KO", "MPEG_TS_SD_KO_T",
                    "MPEG_TS_SD_KO_ISO", "MPEG_TS_JP_T"),
                Video(DlnaMime.VideoMp2T, "video/mp2t", [".ts"],
                    "MPEG_TS_SD_EU", "MPEG_TS_SD_NA", "MPEG_TS_HD_NA", "MPEG_TS_HD_EU"),
                Video(DlnaMime.VideoXMatroska, "video/x-matroska", [".mkv"], "MATROSKA"),
                Video(DlnaMime.VideoXMsvideo, "video/x-msvideo", [".avi"], "MSVIDEO"),
                Video(DlnaMime.VideoAvi, "video/avi", [], "AVI", "AVI_HD"),
                Video(DlnaMime.VideoQuicktime, "video/quicktime", [".mov", ".qt", ".moov"], "QT", "QT_HD"),
                Video(DlnaMime.VideoXMswmv, "video/x-ms-wmv", [".wmv"],
                    "WMV", "WMV_HD", "WMV_FULL", "WMV_BASE", "WMVHIGH_FULL", "WMVHIGH_BASE", "WMVHIGH_PRO",
                    "WMVMED_FULL", "WMVMED_BASE", "WMVMED_PRO", "VC1_ASF_AP_L1_WMA", "VC1_ASF_AP_L2_WMA",
                    "VC1_ASF_AP_L3_WMA"),
                Video(DlnaMime.VideoXMsAsf, "video/x-ms-asf", [".asf", ".asx"], "WMV", "WMV_HD"),
                Video(DlnaMime.VideoXFlv, "video/x-flv", [".flv"], "FLV"),
                Video(DlnaMime.Video3gpp, "video/3gpp", [".3gp"],
                    "AVC_MP4_BL_CIF15_AAC_520", "MPEG4_P2_3GPP_SP_L0B_AMR", "AVC_3GPP_BL_QCIF15_AAC",
                    "MPEG4_H263_3GPP_P0_L10_AMR", "MPEG4_H263_MP4_P0_L10_AAC", "MPEG4_P2_3GPP_SP_L0B_AAC"),
                Video(DlnaMime.VideoWebm, "video/webm", [".webm"], "WEBM"),
                Video(DlnaMime.VideoOgg, "video/ogg", [".ogv"]),

                // Audio
                Audio(DlnaMime.AudioMpeg3, "audio/mpeg3", [".mp3"], "MP3"),
                Audio(DlnaMime.AudioMpeg, "audio/mpeg", [".m2a", ".mpga"], "MPEG", "MP2_MPS"),
                Audio(DlnaMime.AudioMp4, "audio/mp4", [".m4a", ".m4b", ".m4p", ".m4r"], "MP4"),
                Audio(DlnaMime.AudioAac, "audio/aac", [".aac"], "AAC"),
                Audio(DlnaMime.AudioFlac, "audio/flac", [".flac"], "FLAC"),
                Audio(DlnaMime.AudioOgg, "audio/ogg", [".ogg", ".oga"], "OGG"),
                Audio(DlnaMime.AudioWav, "audio/wav", [".wav"], "WAVE"),
                Audio(DlnaMime.AudioXWav, "audio/x-wav", [], "XWAVE"),
                Audio(DlnaMime.AudioXAiff, "audio/x-aiff", [".aiff", ".aif", ".aifc"], "XAIF"),
                Audio(DlnaMime.AudioXMswma, "audio/x-ms-wma", [".wma"], "XWMA"),

                // Image
                Image(DlnaMime.ImageJpeg, "image/jpeg", [".jpg", ".jpeg", ".jpe", ".jfif"],
                    "JPEG", "JPEG_LRG", "JPEG_MED", "JPEG_SM", "JPEG_TN"),
                Image(DlnaMime.ImagePng, "image/png", [".png"], "PNG", "PNG_LRG", "PNG_MED", "PNG_SM", "PNG_TN"),
                Image(DlnaMime.ImageGif, "image/gif", [".gif"], "GIF", "GIF_LRG", "GIF_MED", "GIF_SM"),
                Image(DlnaMime.ImageBmp, "image/bmp", [".bmp", ".bm"], "BMP"),
                Image(DlnaMime.ImageTiff, "image/tiff", [".tif", ".tiff"], "TIFF"),
                Image(DlnaMime.ImageWebp, "image/webp", [".webp"], "WEBP"),
                Image(DlnaMime.ImageXIcon, "image/x-icon", [".ico"], "XICON"),
                Image(DlnaMime.ImageSvgXml, "image/svg+xml", [".svg"], "SVGXML"),

                // Subtitle
                Subtitle(DlnaMime.SubtitleSubrip, "text/srt", [".srt"]),
                Subtitle(DlnaMime.SubtitleXSubrip, "application/x-subrip", [], "SRT"),
                Subtitle(DlnaMime.SubtitleVtt, "text/vtt", [".vtt"]),
                Subtitle(DlnaMime.SubtitleTtmlXml, "application/ttml+xml", [".ttml"]),
                Subtitle(DlnaMime.SubtitleMicroDVD, "text/vnd.dlna.sub-title", [".sub"]),

                // Video - the rest of the reference's table, added 2026-09-04
                Video(DlnaMime.VideoAnimaflex, "video/animaflex", [".afl"]),
                Video(DlnaMime.VideoAvsVideo, "video/avs-video", [".avs"]),
                Video(DlnaMime.VideoDl, "video/dl", [".dl"]),
                Video(DlnaMime.VideoFli, "video/fli", [".fli"]),
                Video(DlnaMime.VideoGl, "video/gl", [".gl"]),
                Video(DlnaMime.VideoMsvideo, "video/msvideo", []),
                Video(DlnaMime.VideoVdo, "video/vdo", [".vdo"]),
                Video(DlnaMime.VideoVivo, "video/vivo", [".viv", ".vivo"]),
                Video(DlnaMime.VideoVndRnRealvideo, "video/vnd.rn-realvideo", [".rv"]),
                Video(DlnaMime.VideoVndVivo, "video/vnd.vivo", [".viv", ".vivo"]),
                Video(DlnaMime.VideoVosaic, "video/vosaic", [".vos"]),
                Video(DlnaMime.VideoXAmtDemorun, "video/x-amt-demorun", [".xdr"]),
                Video(DlnaMime.VideoXAmtShowrun, "video/x-amt-showrun", [".xsr"]),
                Video(DlnaMime.VideoXAtomic3DFeature, "video/x-atomic3d-feature", [".fmf"]),
                Video(DlnaMime.VideoXDl, "video/x-dl", [".dl"]),
                Video(DlnaMime.VideoXDv, "video/x-dv", [".dif", ".dv"], "DVR_MS_VIDEO"),
                Video(DlnaMime.VideoXFli, "video/x-fli", [".fli"]),
                Video(DlnaMime.VideoXGl, "video/x-gl", [".gl"]),
                Video(DlnaMime.VideoXIsvideo, "video/x-isvideo", [".isu"]),
                Video(DlnaMime.VideoXMotionJpeg, "video/x-motion-jpeg", [".mjpg"], "MJPEG"),
                Video(DlnaMime.VideoXMpeg, "video/x-mpeg", [".mp2"], "MPEG_PS_PAL", "MPEG_PS_NTSC"),
                Video(DlnaMime.VideoXMpeq2A, "video/x-mpeq2a", [".mp2"]),
                Video(DlnaMime.VideoXMsAsfPlugin, "video/x-ms-asf-plugin", []),
                Video(DlnaMime.VideoXQtc, "video/x-qtc", [".qtc"]),
                Video(DlnaMime.VideoXScm, "video/x-scm", [".scm"]),
                Video(DlnaMime.VideoXSgiMovie, "video/x-sgi-movie", [".movie", ".mv"]),
                Video(DlnaMime.VideoWindowsMetafile, "windows/metafile", [".wmf"]),
                Video(DlnaMime.VideoXglMovie, "xgl/movie", [".xmz"]),

                // Audio - the rest of the reference's table, added 2026-09-04
                Audio(DlnaMime.AudioAiff, "audio/aiff", [], "AIF"),
                Audio(DlnaMime.AudioBasic, "audio/basic", [".au", ".snd"], "SND"),
                Audio(DlnaMime.AudioIt, "audio/it", [".it"], "IT"),
                Audio(DlnaMime.AudioMake, "audio/make", [".funk", ".my", ".pfunk"], "MAKE"),
                Audio(DlnaMime.AudioMid, "audio/mid", [".rmi"], "MIDI"),
                Audio(DlnaMime.AudioMidi, "audio/midi", [".kar", ".mid", ".midi"], "MIDI"),
                Audio(DlnaMime.AudioMod, "audio/mod", [".mod"], "MOD"),
                Audio(DlnaMime.AudioNspaudio, "audio/nspaudio", [".la", ".lma"], "NSP"),
                Audio(DlnaMime.AudioS3M, "audio/s3m", [".s3m"], "S3M"),
                Audio(DlnaMime.AudioTspAudio, "audio/tsp-audio", [".tsi"], "TSPA"),
                Audio(DlnaMime.AudioTsplayer, "audio/tsplayer", [".tsp"], "TSPL"),
                Audio(DlnaMime.AudioVndQcelp, "audio/vnd.qcelp", [".qcp"], "QCELP"),
                Audio(DlnaMime.AudioVoc, "audio/voc", [".voc"], "VOC"),
                Audio(DlnaMime.AudioVoxware, "audio/voxware", [".vox"], "VOX"),
                Audio(DlnaMime.AudioXAdpcm, "audio/x-adpcm", [".snd"], "XADPCM"),
                Audio(DlnaMime.AudioXAu, "audio/x-au", [".au"], "XAU"),
                Audio(DlnaMime.AudioXGsm, "audio/x-gsm", [".gsd", ".gsm"], "XGSM"),
                Audio(DlnaMime.AudioXJam, "audio/x-jam", [".jam"], "XJAM"),
                Audio(DlnaMime.AudioXLiveaudio, "audio/x-liveaudio", [".lam"], "XLA"),
                Audio(DlnaMime.AudioXMatroska, "audio/x-matroska", [".mka"], "XMKV"),
                Audio(DlnaMime.AudioXMid, "audio/x-mid", [".mid", ".midi"], "XMID"),
                Audio(DlnaMime.AudioXMidi, "audio/x-midi", [".mid", ".midi"], "XMIDI"),
                Audio(DlnaMime.AudioXMod, "audio/x-mod", [".mod"], "XMOD"),
                Audio(DlnaMime.AudioXMpeg, "audio/x-mpeg", [".mp2"], "XMPEG"),
                Audio(DlnaMime.AudioXMpeg3, "audio/x-mpeg-3", [], "XMP3"),
                Audio(DlnaMime.AudioXMpequrl, "audio/x-mpequrl", [".m3u"], "XMPQ"),
                Audio(DlnaMime.AudioXNspaudio, "audio/x-nspaudio", [".la", ".lma"], "XNSPA"),
                Audio(DlnaMime.AudioXPnRealaudio, "audio/x-pn-realaudio", [".ra", ".ram", ".rm", ".rmm", ".rmp"],
                    "XREAL"),
                Audio(DlnaMime.AudioXPnRealaudioPlugin, "audio/x-pn-realaudio-plugin", [".ra", ".rmp", ".rpm"], "XREALP"),
                Audio(DlnaMime.AudioXPsid, "audio/x-psid", [".sid"], "XPSID"),
                Audio(DlnaMime.AudioXRealaudio, "audio/x-realaudio", [".ra"], "XREALA"),
                Audio(DlnaMime.AudioXTwinvq, "audio/x-twinvq", [".vqf"], "XTWINVQ"),
                Audio(DlnaMime.AudioXTwinvqPlugin, "audio/x-twinvq-plugin", [".vqe", ".vql"], "XTWINVQP"),
                Audio(DlnaMime.AudioXVndAudioexplosionMjuicemediafile, "audio/x-vnd.audioexplosion.mjuicemediafile", [".mjf"],
                    "XVND"),
                Audio(DlnaMime.AudioXVoc, "audio/x-voc", [".voc"], "XVOC"),
                Audio(DlnaMime.AudioXm, "audio/xm", [".xm"], "XM"),
                Audio(DlnaMime.AudioMusicCrescendo, "music/crescendo", [".mid", ".midi"], "CRESCENDO"),
                Audio(DlnaMime.AudioXMusicXMidi, "x-music/x-midi", [".mid", ".midi"], "XMIDI"),

                // Image - the rest of the reference's table, added 2026-09-04
                Image(DlnaMime.ImageCmuRaster, "image/cmu-raster", [".ras", ".rast"], "CMURASTER"),
                Image(DlnaMime.ImageFif, "image/fif", [".fif"], "FIF"),
                Image(DlnaMime.ImageFlorian, "image/florian", [".flo", ".turbot"], "FLORIAN"),
                Image(DlnaMime.ImageG3Fax, "image/g3fax", [".g3"], "G3FAX"),
                Image(DlnaMime.ImageIef, "image/ief", [".ief", ".iefs"], "IEF"),
                Image(DlnaMime.ImageJutvision, "image/jutvision", [".jut"], "JUTVISION"),
                Image(DlnaMime.ImageNaplps, "image/naplps", [".nap", ".naplps"], "NAPLPS"),
                Image(DlnaMime.ImagePict, "image/pict", [".pic", ".pict"], "PICT"),
                Image(DlnaMime.ImagePjpeg, "image/pjpeg", [], "PJPEG"),
                Image(DlnaMime.ImageVasa, "image/vasa", [".mcf"], "VASA"),
                Image(DlnaMime.ImageVndDwg, "image/vnd.dwg", [".dwg", ".dxf", ".svf"], "VNDDWG"),
                Image(DlnaMime.ImageVndFpx, "image/vnd.fpx", [".fpx", ".vnd.net-fpx"], "VNDFPX"),
                Image(DlnaMime.ImageVndRnRealflash, "image/vnd.rn-realflash", [".rf"], "VNDRNREALFLASH"),
                Image(DlnaMime.ImageVndRnRealpix, "image/vnd.rn-realpix", [".rp"], "VNDRNREALPIX"),
                Image(DlnaMime.ImageVndWapWbmp, "image/vnd.wap.wbmp", [".wbmp"], "VNDWAPWBMP"),
                Image(DlnaMime.ImageVndXiff, "image/vnd.xiff", [".xif"], "VNDXIFF"),
                Image(DlnaMime.ImageXCmuRaster, "image/x-cmu-raster", [".ras"], "XCMURASTER"),
                Image(DlnaMime.ImageXDwg, "image/x-dwg", [".dwg", ".dxf", ".svf"], "XDWG"),
                Image(DlnaMime.ImageXJg, "image/x-jg", [".art"], "XJG"),
                Image(DlnaMime.ImageXJps, "image/x-jps", [".jps"], "XJPS"),
                Image(DlnaMime.ImageXNiff, "image/x-niff", [".nif", ".niff"], "XNIFF"),
                Image(DlnaMime.ImageXPcx, "image/x-pcx", [".pcx"], "XPCX"),
                Image(DlnaMime.ImageXPict, "image/x-pict", [".pct", ".pict"], "XPICT"),
                Image(DlnaMime.ImageXPortableAnymap, "image/x-portable-anymap", [".pnm"], "XPORTABLEANYMAP"),
                Image(DlnaMime.ImageXPortableBitmap, "image/x-portable-bitmap", [".pbm"], "XPORTABLEBITMAP"),
                Image(DlnaMime.ImageXPortableGraymap, "image/x-portable-graymap", [".pgm"], "XPORTABLEGRAYMAP"),
                Image(DlnaMime.ImageXPortablePixmap, "image/x-portable-pixmap", [".ppm"], "XPORTABLEPIXMAP"),
                Image(DlnaMime.ImageXQuicktime, "image/x-quicktime", [".qif", ".qti", ".qtif"], "XQUICKTIME"),
                Image(DlnaMime.ImageXRgb, "image/x-rgb", [".rgb"], "XRGB"),
                Image(DlnaMime.ImageXTiff, "image/x-tiff", [], "XTIFF"),
                Image(DlnaMime.ImageXWindowsBmp, "image/x-windows-bmp", [], "XWINDOWSBMP"),
                Image(DlnaMime.ImageXXbitmap, "image/x-xbitmap", [".xbm"], "XXBITMAP"),
                Image(DlnaMime.ImageXXbm, "image/x-xbm", [".xbm"], "XXBM"),
                Image(DlnaMime.ImageXXpixmap, "image/x-xpixmap", [".pm", ".xpm"], "XXPIXMAP"),
                Image(DlnaMime.ImageXXwd, "image/x-xwd", [".xwd"], "XXWD"),
                Image(DlnaMime.ImageXXwindowdump, "image/x-xwindowdump", [".xwd"], "XXWINDOWDUMP"),
            ];

            return entries.ToFrozenDictionary(static e => e.Mime);
        }

        private static DlnaMimeInfo Video(DlnaMime mime, string mimeString, string[] extensions, params string[] profiles)
        {
            return new DlnaMimeInfo(mime, mimeString, DlnaMedia.Video, profiles, extensions);
        }

        private static DlnaMimeInfo Audio(DlnaMime mime, string mimeString, string[] extensions, params string[] profiles)
        {
            return new DlnaMimeInfo(mime, mimeString, DlnaMedia.Audio, profiles, extensions);
        }

        private static DlnaMimeInfo Image(DlnaMime mime, string mimeString, string[] extensions, params string[] profiles)
        {
            return new DlnaMimeInfo(mime, mimeString, DlnaMedia.Image, profiles, extensions);
        }

        private static DlnaMimeInfo Subtitle(DlnaMime mime, string mimeString, string[] extensions, params string[] profiles)
        {
            return new DlnaMimeInfo(mime, mimeString, DlnaMedia.Subtitle, profiles, extensions);
        }
    }
}
