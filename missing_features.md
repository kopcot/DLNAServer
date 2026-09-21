# Missing features and reported bugs

Working inbox. Add reports here; each batch is folded into `PLAN.md` section 6 as it closes,
which is where the reasoning and the verification end up.

## Open

*(nothing open)*

## Closed batches

| Raised | Closed | Items | Recorded in |
| --- | --- | --- | --- |
| 2026-09-02 | mostly | first batch | `PLAN.md` 6b |
| 2026-09-03 | all | second batch | `PLAN.md` 6c |
| 2026-09-03 | 2026-09-04 | third batch | `PLAN.md` 6f |
| 2026-09-06 | 2026-09-08 | cache-limit exclusion, move creates a new record, embedded album art, music icon, media-kind badge | `PLAN.md` 6g |
| 2026-09-12 | 2026-09-12 | photo opens larger (desktop overlay with zoom / phone page), *This folder* and *This file* start shut, *This file* above *File*, Reset reopens the filters, Help and About pages, copyright line | `PLAN.md` 6m |
| 2026-09-15 | 2026-09-15 | collapsible folders and files, upload page with settings / remembered destination / security log / overwrite-skip / report, provenance tooltips behind a temporary switch, assembly table on About, version notes file | `PLAN.md` 6n |
| 2026-09-16 | 2026-09-17 | deleting media leaves its preview behind, edit a file's type and compatibility profile, reset *Keep in memory*, search filter for *Keep in memory* (upload-folder check was already closed by 6n) | `PLAN.md` 6o |

## Closed 2026-09-17, as raised

1) Deleting media is not deleting thumbnails automatically 
2) On the preview page, example: http://192.168.1.200:26853/admin/preview/23f33794-03bb-468f-4df2-08df13af292c , in the "File"-group , add option to edit for changing "Compatibility profile" and "File type"
3) On the preview page, example: http://192.168.1.200:26853/admin/preview/23f33794-03bb-468f-4df2-08df13af292c , in the "This file"-group , add option to reset "Keep in memory"
4) On the Search files page, example: http://192.168.1.200:26853/admin/library/search/files , add filter-option for search for "Keep in memory"
5) on the Settings page, example: http://192.168.1.200:26853/admin/settings , add option for checking existence of "Upload folder" (same as for "Source folders")
   - **already shipped 2026-09-16**, `PLAN.md` 6n under "Checking the upload folder". Nothing was written
     for it in this batch; the row had simply not been struck off.

## Closed 2026-09-15, as raised

1) In the librabry , Folders and files should be collapseable 
2) Add option in the main menu on admin portal, to upload multiple files , there should be option to select source files and destination folder , take in mind, that it should work on the windows and linux servers, docker and as source can be linux, windows, android , etc. browser type 
2a) Upload option can be disabled from settings (with restart needed and note)
2b) Adding is only available to specific folder - addtional option in settings
2c) Prefilled folder should be stored on the browser side, so next time will not be needed to select it again + it should be stored in the database with info , about IP, browser type and any other ifno, which will help to identify , where that user with that device and browser last time uploaded files, so it will be preset for him in the case, that he will delete his browser local data
2d) For upload files , there should be stored log in separate log-file, with IP, browser, OS-type, language settings and all max. possible info about uploader, file info , etc. - this is for security reasons (make a comment for it)
2e) On the upload page, an option to overwrite an existing file or to skip it
2f) After the uploads, a collapseable report-table - one row per file with success / failed / overwritten / skipped
3) When I hover the any non-static text, I want to see tooltip with technical info, from where that number is comming, for example: when I hover over group "Total in use 593.6 MB" - I want to see tooltip "Working Set (MB)", another example: "Tidy-ups so far 22937 / 1643 / 119" - I want to see tooltip with "GC Collections Gen0/Gen1/Gen2" , another example: "Contact mailto:kopco.t@gmail.com" - I want to see tooltip "config.json Dlna.Server.ManufacturerUrl" , and so on , this option is disabled as default and can be temmporary enabled by a new Settings-Temporary option 
3a) make a component for it
4) Add a new collapseable-group in the about page with table and all used assemblies and their versions
5) Version notes file - one entry per assembly-version bump, newest at the top, saying what changed in that version
