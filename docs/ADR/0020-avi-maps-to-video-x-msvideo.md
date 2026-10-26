# 0020. `.avi` maps to `video/x-msvideo`

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 20

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, while closing the last of the review documents deleted that day. Investigated because an AVI would not play in the admin preview and a wrong MIME was the obvious suspect.

## Decision

**`.avi` maps to `video/x-msvideo` and that is CORRECT - do not "fix" it.** It is not wrong, and no MIME could be: **Chrome ignores the declared `Content-Type` for media and sniffs the bytes.** Proved with a natural experiment already in the library - the LG workaround maps `.mp3 -> AudioMp4`, so a real MP3 (`ID3` header) is served as `audio/mp4`, and it plays to `readyState 4` with a 2356 s duration. Meanwhile every spelling of the AVI type returns `""` from `canPlayType`: `video/x-msvideo`, `video/avi`, `video/vnd.avi`, `video/msvideo`. Chrome has no AVI demuxer, and the error is `DEMUXER_ERROR_COULD_NOT_OPEN: FFmpegDemuxer: open context failed`.

Also ruled out by measurement, not reasoning: delivery (206, correct `Content-Range` over 734,457,166 bytes, intact `RIFF....AVI` payload, and an MP4 loading through the same endpoint on the same page seconds later), file size (105 MB to 2.64 GB all fail), non-ASCII filenames, the byte cache (warmed a 105 MB AVI into memory and confirmed it in `/manage/filecache` - identical failure), and mislabelled containers (44 of 45 sampled `.avi` files are genuine `RIFF....AVI`).

**The reference ships the same value** (`ServerConfig.cs:100`), which is what televisions were validated against; IANA registers `video/vnd.avi` and `video/x-msvideo` is the unregistered conventional alias that Apache and nginx ship. Changing it gains nothing and risks the one path that works. `DLNA.ORG_PN=MSVIDEO` is likewise not a real DLNA profile - the spec has none for AVI, the reference used the string anyway, and sets play AVI on their own codec support.

## Consequences

**If a television ever refuses an AVI**, blanking `ProfileName` for `.avi` is the lever (6f: a blank and a null profile are identical on the wire). Televisions are unaffected by any of this - it is a browser limit.
