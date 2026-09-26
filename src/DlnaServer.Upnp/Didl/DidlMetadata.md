# DIDL-Lite metadata notes

Working notes for the types in this folder — `DidlItem`, `DidlContainer`, `DidlResource`, `DidlDocument`,
`DidlSerializer`. They answer one question: **what does a property here become on the wire, and does any
renderer care?**

The content comes from the reference server's
`DLNAServer/SOAP/Endpoints/Responses/ContentDirectory/BrowseItem.cs`, which carries a large commented table
of DLNA/UPnP metadata names at the bottom of the file plus a per-property XML-doc note. That server is
read-only and lives outside this repository (`T:\repos\DLNAServer_Legacy`), and that table is the clearest piece of
research in it, so it is reproduced here rather than left where nothing points at it.

**Read the caveat in section 4 before treating the big table as a specification.** It is the reference
author's survey of names, not the UPnP ContentDirectory spec, and a number of its rows are not real
DIDL-Lite properties.

---

## 1. What this implementation emits today

Element order is fixed by the `Order` values on the attributes and it matters — renderers parse
positionally more often than the specification would suggest. The ordering reproduces the reference
exactly, which is why the numbers have gaps: they are the reference's own, and closing the gaps would
change nothing but would lose the correspondence.

### `DidlItem` — `<item>` in the `DIDL-Lite` namespace

| Wire name | Order | Kind | Property | Meaning |
| --- | --- | --- | --- | --- |
| `@id` | — | attribute | `ObjectId` | Identifier the renderer sends back to browse or play this item. Carries the file's `PublicId`. |
| `@parentID` | — | attribute | `ParentId` | Container the item belongs to; `0` is the root. |
| `@restricted` | — | attribute | `Restricted` | Always `1` — the renderer may not modify anything on a read-only server. |
| `res` | 0 | element | `Resources` | The media resource: the URL the renderer fetches to play the file. |
| `upnp:class` | 2 | element | `Class` | Kind of item, e.g. `object.item.videoItem`. Decides where a renderer files it. |
| `dc:title` | 4 | element | `Title` | Name shown in the renderer's list. |
| `dc:date` | 5 | element | `Date` | Creation date of the file, ISO-8601 round-trip form. |
| `upnp:videoCodec` | 9 | element | `VideoCodec` | Codec of the video track, e.g. `h264`. |
| `upnp:audioCodec` | 10 | element | `AudioCodec` | Codec of the audio track, e.g. `aac`. |
| `res` | 100 | element | `ThumbnailResource` | A second `res`, the thumbnail, marked `DLNA.ORG_CI=1`. |
| `upnp:albumArtURI` | 101 | element | `AlbumArtUri` | URL of the item's preview image. |
| `upnp:icon` | 102 | element | `Icon` | Same URL again; some renderers read only one of the two. |

### `DidlContainer` — `<container>`

| Wire name | Order | Kind | Property | Meaning |
| --- | --- | --- | --- | --- |
| `@id` | — | attribute | `ObjectId` | Directory's `PublicId`. |
| `@parentID` | — | attribute | `ParentId` | Parent directory, or `0` at the root. |
| `@restricted` | — | attribute | `Restricted` | Always `1`. |
| `@searchable` | — | attribute | `Searchable` | `1` — the container may be the target of a Search. |
| `@childCount` | — | attribute | `ChildCount` | Number of children, when known. |
| `upnp:class` | 2 | element | `Class` | `object.container.storageFolder`. |
| `dc:title` | 4 | element | `Title` | Folder name. |
| `upnp:albumArtURI` | 101 | element | `AlbumArtUri` | Folder icon. |
| `upnp:icon` | 102 | element | `Icon` | Folder icon again. |

### `DidlResource` — `<res>`

The element's **text** is the absolute URL to fetch; everything else is an attribute.

| Wire name | Property | Meaning |
| --- | --- | --- |
| `@protocolInfo` | `ProtocolInfo` | Four colon-separated fields describing how the resource may be fetched and decoded. A renderer matches this against its own capabilities before it will play anything — see `Constants/DlnaProtocolInfo.cs`, and note the same feature list is sent as the `contentFeatures.dlna.org` response header. |
| `@size` | `Size` | Size in bytes, for progress and buffering. |
| `@duration` | `Duration` | Playing time as `H:mm:ss.fff`, hours unbounded rather than wrapping at 24. |
| `@resolution` | `Resolution` | Pixel dimensions as `WIDTHxHEIGHT`. |
| `@bitrate` | `Bitrate` | **Bytes** per second, not bits — this is the DIDL-Lite definition and a frequent source of wrong values. |
| `@nrAudioChannels` | `AudioChannels` | Channel count, e.g. `2` for stereo. |
| `@sampleFrequency` | `SampleFrequency` | Audio sample rate in hertz. |
| *(element text)* | `Url` | Built against the local address the request arrived on, so a multi-homed server hands each renderer a URL reachable from the interface it is using. |

---

## 2. Declared by the reference, deliberately absent here

| Wire name | Reference | Here | Why |
| --- | --- | --- | --- |
| `upnp:comments` | Property declared, `Order = 6` | Not declared | Never assigned by `BrowseItemMapper`, so it never reached the wire. Nothing indexes comments. |
| `upnp:genre` | Property declared, `Order = 7` | Not declared | Same — declared and never populated. Also not a name any renderer in use reads. |
| `res@subtitlesType` | Attribute declared | Not declared | Never assigned. Linked subtitle files are advertised another way since 2026-09-26 - an extra `res` each, plus `sec:CaptionInfoEx` (see `docs/dlna-compatibility.md`). Tracks inside the file are still indexed and not advertised. |
| `res@language` | Attribute declared | Not declared | Never assigned. |
| `res@class` | Attribute declared **and populated** with `video` / `audio` / `image` / `subtitle` | Not declared | The one in this table the reference actually emits. Not a DIDL-Lite attribute: the item's kind is carried by `upnp:class`, which every renderer reads. Left out as duplication; if a device is ever found to want it, this row is the note to reverse. |

`res@subtitles` appears in the reference's own research table but was never declared as a property at all.

---

## 3. The reference's metadata survey

Reproduced from the comment block at the bottom of `BrowseItem.cs`. Columns are the reference author's:
which media kinds a name is said to apply to, its XML prefix-name, and its description. The **Here**
column is added — what this implementation actually emits, from section 1.

| Metadata | Generic | Video | Audio | Photo | Other | XML prefix-name | Here | Description |
| --- | :-: | :-: | :-: | :-: | :-: | --- | :-: | --- |
| Title | ✔ | ✔ | ✔ | ✔ | ✔ | `dc:title` | ✔ | The title of the media item |
| Copyright | ✔ | ✔ | ✔ | ✔ | ✔ | `dc:rights` | – | Copyright information for the media item |
| Original Release Date | ✔ | ✔ | ✔ | ✔ | ✔ | `dc:originalReleaseDate` | – | The original release date of the media item |
| Language Code | ✔ | ✔ | ✔ | ✔ | ✔ | `dc:language` | – | Language code for the media (e.g. ISO 639-2) |
| Year | ✔ | ✔ | ✔ | ✔ | ✔ | `dc:date` | ✔ | The year the media was created or released |
| Date Added | ✔ | ✔ | ✔ | ✔ | ✔ | `dc:date` | ✔ | The date the item was added or created |
| Language | ✔ | ✔ | ✔ | ✘ | ✔ | `dc:language` | – | The language of the media (audio, text, etc.) |
| Artist/Author | ✔ | ✔ | ✔ | ✔ | ✔ | `dc:creator` | – | The artist or author of the media item |
| File Path/URL | ✔ | ✔ | ✔ | ✔ | ✔ | `res` | ✔ | The resource URL for media streaming |
| Bitrate | ✘ | ✔ | ✔ | ✘ | ✘ | `res@bitrate` | ✔ | The bitrate of the video or audio media |
| File Format | ✔ | ✔ | ✔ | ✔ | ✔ | `res@class` | – | The type and format of the media (e.g. audio, video, image) |
| Duration | ✔ | ✔ | ✔ | ✘ | possibly ✔ | `res@duration` | ✔ | The length of the media playback |
| Frame Rate | ✘ | ✔ | ✘ | ✘ | ✘ | `res@framerate` | – | The frame rate of the video (e.g. 24fps, 30fps) |
| Audio Channels | ✘ | ✔ | ✔ | ✘ | ✘ | `res@nrAudioChannels` | ✔ | The number of audio channels (e.g. stereo, 5.1) |
| Codec | ✘ | ✔ | ✔ | ✘ | ✘ | `res@protocolInfo` | ✔ | The codec used for encoding the media (e.g. H.264, MP3) |
| Resolution | ✘ | ✔ | ✘ | ✔ | ✘ | `res@resolution` | ✔ | The resolution of the video or image (e.g. 1920x1080) |
| Sample Rate | ✘ | ✘ | ✔ | ✘ | ✘ | `res@sampleFrequency` | ✔ | The sample rate of the audio media (e.g. 44.1kHz) |
| Subtitles | ✘ | ✔ | ✘ | ✘ | ✘ | `res@subtitles` | – | The subtitle track URL or file for videos |
| Album Name | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:album` | – | The album name of the audio track |
| Thumbnail | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:albumArtURI` | ✔ | The URL for the media's thumbnail image |
| Album Art | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:albumArtURI` | ✔ | URL to the album artwork for audio files |
| Aperture | ✘ | ✘ | ✘ | ✔ | ✘ | `upnp:aperture` | – | The aperture value for the photo |
| File Size | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:size` | via `res@size` | The size of the media file in bytes |
| Genre | ✔ | ✔ | ✔ | ✘ | ✔ | `upnp:genre` | – | The genre of the media content |
| Aspect Ratio | ✘ | ✔ | ✘ | ✔ | ✘ | `upnp:longDescription` | – | The aspect ratio of the video or image (e.g. 16:9) |
| Track Number | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:originalTrackNumber` | – | The track number within an album for audio files |
| Camera Model | ✘ | ✘ | ✘ | ✔ | ✘ | `upnp:modelName` | – | The camera model used to take the photo |
| Exposure Time | ✘ | ✘ | ✘ | ✔ | ✘ | `upnp:exposureTime` | – | The exposure time for the photo |
| ISO Speed | ✘ | ✘ | ✘ | ✔ | ✘ | `upnp:isoSpeed` | – | The ISO speed used for the photo |
| GPS Location | ✘ | ✘ | ✘ | ✔ | ✘ | `upnp:location` | – | The GPS coordinates where the photo was taken |
| Description | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:description` | – | A short description of the media item |
| Mood | ✔ | ✘ | ✔ | ✘ | ✔ | `upnp:mood` | – | The mood or emotion associated with the media |
| Composer | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:composer` | – | The composer of the audio track |
| Series Title | ✘ | ✔ | ✘ | ✘ | ✘ | `upnp:seriesTitle` | – | The title of the series if the video is part of one |
| Season Number | ✘ | ✔ | ✘ | ✘ | ✘ | `upnp:seasonNumber` | – | The season number if the video is part of a series |
| Episode Number | ✘ | ✔ | ✘ | ✘ | ✘ | `upnp:episodeNumber` | – | The episode number if the video is part of a series |
| Production Year | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:productionYear` | – | The year the media was produced or created |
| Country | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:country` | – | The country where the media was produced |
| Rating | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:rating` | – | Rating of the media (e.g. MPAA rating for movies) |
| Contributors | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:contributor` | – | Other contributors (e.g. directors, producers) |
| Keywords | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:keywords` | – | Keywords for better searchability |
| Related Items | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:relatedItems` | – | Links to related media items |
| Custom Properties | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:customProperty` | – | Any custom properties specific to your application |
| Cover Art | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:coverArt` | – | A URI for the cover art of an audio track |
| Video Format | ✘ | ✔ | ✘ | ✘ | ✘ | `upnp:videoFormat` | – | The format of the video (e.g. 4K, HD, SD) |
| Audio Format | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:audioFormat` | – | The format of the audio (e.g. Lossless, AAC, MP3) |
| Color Depth | ✘ | ✘ | ✘ | ✔ | ✘ | `upnp:colorDepth` | – | The color depth of the photo (e.g. 24-bit) |
| File Size (Compressed) | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:compressedSize` | – | The size of the compressed file, if applicable |
| Playback Position | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:playbackPosition` | – | The current playback position (e.g. in seconds) |
| Last Played Date | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:lastPlayed` | – | The date the media item was last played |
| Play Count | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:playCount` | – | The number of times the item has been played |
| User Rating | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:userRating` | – | The rating given by users (e.g. out of 5 stars) |
| Stream Type | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:streamType` | – | The type of streaming (e.g. live, on-demand) |
| Parental Rating | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:parentalRating` | – | The parental guidance rating |
| Recording Date | ✔ | ✔ | ✘ | ✔ | ✔ | `upnp:recordingDate` | – | The date the media was recorded or captured |
| Device Model | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:deviceModel` | – | The model of the device that created the media |
| Device Manufacturer | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:deviceManufacturer` | – | The manufacturer of that device |
| Video Quality | ✘ | ✔ | ✘ | ✘ | ✘ | `upnp:videoQuality` | – | The quality setting of the video |
| Audio Quality | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:audioQuality` | – | The quality of the audio track |
| Comments | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:comments` | – | User comments associated with the media item |
| License | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:license` | – | Licensing information for the media |
| Original Format | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:originalFormat` | – | The original format before any conversion |
| Video Orientation | ✘ | ✔ | ✘ | ✔ | ✘ | `upnp:orientation` | – | The orientation of the video (landscape, portrait) |
| Color Space | ✘ | ✔ | ✘ | ✘ | ✘ | `upnp:colorSpace` | – | The color space used (e.g. sRGB, Adobe RGB) |
| Location Description | ✘ | ✘ | ✘ | ✔ | ✘ | `upnp:locationDescription` | – | A textual description of where the photo was taken |
| Featured Artists | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:featuredArtists` | – | Artists featured in an audio track |
| Remixers | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:remixers` | – | Individuals who have remixed the audio track |
| Instrumental Version | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:instrumentalVersion` | – | Whether the track is an instrumental version |
| Video Tagging | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:videoTagging` | – | Tags on the video content for easier categorisation |
| Collection Name | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:collectionName` | – | The collection the media belongs to |
| Bitrate | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:bitrate` | via `res@bitrate` | The bitrate of the media file |
| Sample Rate | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:sampleRate` | via `res@sampleFrequency` | The sample rate of the audio track |
| Channel Count | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:channelCount` | via `res@nrAudioChannels` | The number of audio channels |
| Aspect Ratio | ✘ | ✔ | ✘ | ✘ | ✘ | `upnp:aspectRatio` | – | The aspect ratio of the video (e.g. 16:9, 4:3) |
| Frame Rate | ✘ | ✔ | ✘ | ✘ | ✘ | `upnp:frameRate` | – | The frame rate of the video |
| Color Model | ✘ | ✔ | ✘ | ✘ | ✘ | `upnp:colorModel` | – | The color model used (e.g. RGB, YUV) |
| Photo Dimensions | ✘ | ✘ | ✘ | ✔ | ✘ | `upnp:photoDimensions` | via `res@resolution` | The dimensions of the photo in pixels |
| Location Coordinates | ✔ | ✘ | ✘ | ✔ | ✘ | `upnp:locationCoordinates` | – | Geographic coordinates (latitude, longitude) |
| Event Date | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:eventDate` | – | The date of the event depicted in the media |
| Duration | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:duration` | via `res@duration` | The total duration of the media item |
| File Format | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:fileFormat` | via `res@protocolInfo` | The file format (e.g. MP4, MP3, JPEG) |
| Editor | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:editor` | – | Who edited the audio track |
| Production Company | ✔ | ✔ | ✘ | ✘ | ✔ | `upnp:productionCompany` | – | The company that produced the media item |
| Soundtrack | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:soundtrack` | – | The soundtrack associated with the audio or video |
| Subtitles Available | ✔ | ✔ | ✘ | ✘ | ✔ | `upnp:subtitlesAvailable` | – | Whether subtitles are available for the video |
| Subtitle Language | ✔ | ✔ | ✘ | ✘ | ✔ | `upnp:subtitleLanguage` | – | The language of the available subtitles |
| Special Features | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:specialFeatures` | – | Commentary, behind-the-scenes and similar |
| Video Codec | ✘ | ✔ | ✘ | ✘ | ✘ | `upnp:videoCodec` | ✔ | The codec used for the video (e.g. H.264, HEVC) |
| Audio Codec | ✘ | ✘ | ✔ | ✘ | ✘ | `upnp:audioCodec` | ✔ | The codec used for the audio (e.g. AAC, MP3) |
| User Tags | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:userTags` | – | Tags created by users |
| Related Media IDs | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:relatedMediaIDs` | – | Identifiers of related media items |
| Broadcast Channel | ✔ | ✔ | ✘ | ✘ | ✔ | `upnp:broadcastChannel` | – | The channel used for broadcasting the video |
| Original Source | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:originalSource` | – | The original source (e.g. VHS, CD) |
| Viewing Instructions | ✔ | ✔ | ✔ | ✔ | ✔ | `upnp:viewingInstructions` | – | Specific instructions for viewing the item |

`res@subtitlesType`, declared on the reference's `Resource` but missing from its table, is documented there
as: the subtitle **format** (`srt`, `vtt`, …) when it is known and embedded in the stream or container, or
the literal `external` when the subtitles are a separate file.

---

## 4. Caveat — the table is research, not the specification

Treat section 3 as a list of candidate names, and check each one before adding it:

- **Many `upnp:` rows are not real DIDL-Lite properties.** `upnp:size`, `upnp:duration`, `upnp:bitrate`,
  `upnp:sampleRate`, `upnp:channelCount`, `upnp:fileFormat`, `upnp:aspectRatio`, `upnp:frameRate`,
  `upnp:photoDimensions`, `upnp:videoCodec`, `upnp:audioCodec` and most of the long tail towards the
  bottom do not exist in the UPnP ContentDirectory specification. The real property is the `res`
  attribute the **Here** column points at. A renderer receiving an unknown element ignores it, so these
  are harmless but useless — bytes on the wire for nothing.
- **Several rows duplicate one another**, sometimes under different names — Bitrate, Duration, Sample
  Rate, File Format and Aspect Ratio each appear twice with different prefixes.
- **One row is plainly wrong:** Aspect Ratio is listed as `upnp:longDescription`, which is a free-text
  description field.
- **`upnp:videoCodec` / `upnp:audioCodec` are emitted here anyway.** They are in the reference's live
  output, and wire compatibility with the working server outranks specification purity — see `docs/decisions.md`
  section 2.

Anything added from this table needs the same treatment as everything else in this folder: a golden test
over the serialised bytes, and a verification against a real renderer.
