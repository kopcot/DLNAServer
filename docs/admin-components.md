# Admin UI components - what exists and where it is used

**Last refreshed: 2026-11-10**, by reading `src/DlnaServer.Admin` and `tests/DlnaServer.UiTests` after the 2.0 restyle (on top of `b29ccdb`).

**Stale check:** on every read, compare the date above with today. More than 14 days old, or older than
the newest commit touching `src/DlnaServer.Admin` (`git log -1 --format=%cs -- src/DlnaServer.Admin`) →
refresh it before relying on it.

**How to apply:** before writing markup on an admin page - a bordered section, a labelled field, a result
message, a table - look here for the shared component that already draws it and use it the way the pages
listed against it do. Every shared component in `Shared/` exists because the same markup had been
hand-written several times and the copies had drifted; adding a fourth copy undoes that. Order: a
component from `Shared/` first, then a native element styled by an existing class in `admin.css`.

**Keep it current:** a change that adds, renames or deletes a page or a shared component, changes a
component's parameters, or changes markup a UI test selects on (see *What the UI tests hold on to*)
updates this file and its date in the same change.

**How to refresh:** re-derive every table from the code, not from the previous version of this file:

- Pages: `@page`, `@rendermode` and `@inherits` at the top of each `Pages/*.razor`, the `PageHeader` title,
  and every `Panel` / `CollapsiblePanel` heading.
- Shared components: each `Shared/*.razor` - its opening `@* ... *@` comment for the description, its
  `[Parameter]` properties, and its first element for the root markup.
- Usage: for each component, the files that contain `<ComponentName` followed by a space, `/` or `>`.
- UI-test hooks: every `By.Id`, `By.CssSelector`, `By.XPath` and `By.TagName` in `tests/DlnaServer.UiTests`.

**Stack:** Blazor Server in a Razor Class Library, one stylesheet (`wwwroot/admin.css`), **no JavaScript of
our own** - `blazor.web.js` is the only script. The router is static; a page that has controls opts in
with `@rendermode InteractiveServer` (see `App.razor` for why the whole router is not interactive).

## Shell

| Piece | File | What it does |
|---|---|---|
| Document | `App.razor` | `<html>` head: `admin.css` linked as `_content/DlnaServer.Admin/admin.css`, favicon from `/admin/favicon.ico`, `blazor.web.js`; `div#components-reconnect-modal`, Blazor's reconnect banner, supplied so the stylesheet draws it (`.reconnect-show` / `.reconnect-failed` / `.reconnect-rejected`) |
| Router | `Routes.razor` | Default layout `MainLayout`; `FocusOnNavigate` focuses the page's `h1`; an unknown `/admin` address renders "Not found" inside the layout |
| Layout | `Shared/MainLayout.razor` | `div.layout` → `nav.sidebar` (brand bar - the device icon `/admin/icon/large.png` and the name, `NavMenu`, `div.sitefoot` copyright) + `main.content` wrapping `@Body` in an `ErrorBoundary` whose error content is a bad `Notice` linking to Settings |
| Phone menu | `Shared/MainLayout.razor` | `details.menubutton` > `summary` in the brand bar - an empty disclosure whose `[open]` shows `.nav` (`.sidebar:has(...)`), so the menu opens and closes without JavaScript and is announced as expanded or collapsed; hidden on a wide screen |
| Navigation | `Shared/NavMenu.razor` | `ul.nav` of `NavLink`s; `li.sub` marks the indented sub-items; *Add files* appears only while uploads are accepted |

## Pages (13)

Render mode: **I** = `InteractiveServer` (a SignalR circuit), **S** = static server rendering. Base:
**APB** = `AdminPageBase` (cancellation, the database gate and the `Message` shown by `Notice`).

| Route | File | Mode | Base | `h1` (PageHeader title) | Panel headings | Shared components used |
|---|---|---|---|---|---|---|
| `/admin` | `Dashboard` | I | APB | Dashboard | Server; Memory; Recently served files; Library | Notice, PageHeader, Panel, StatGrid, StatTile |
| `/admin/library`, `/admin/library/{DirectoryId}` | `Library` | I | APB | Library | Folders (c); Files (c); Recently added (c) | CheckField, CollapsiblePanel, FileList, FolderList, Notice, PageHeader, Panel, ProcessingActions, ShowMore |
| `/admin/library/search/folders` | `SearchFolders` | I | APB | Search folders | (SearchPanel) | FolderList, FormField, PageHeader, SearchPanel |
| `/admin/library/search/files`, `/admin/library/search` | `SearchFiles` | I | APB | Search files | (SearchPanel) | FieldPair, FileList, FormField, PageHeader, SearchPanel |
| `/admin/preview/{FileId}`, `/admin/preview/{DirectoryId}/{FileId}` | `Preview` | I | APB | the file's title | Video; Subtitles; facts and actions panels from the components | DataTable, FileTypeEditor, MediaFileFacts, MetaList, MetaRow, Notice, PageHeader, Panel, ProcessingActions, SubtitleEditor |
| `/admin/photo/{FileId}`, `/admin/photo/{DirectoryId}/{FileId}` | `Photo` | S | APB | Photo | - | PageHeader, Panel |
| `/admin/upload` | `Upload` | S | - | Add files | What was uploaded (after an upload, above the form, with a bad Notice when it was refused); Choose the files | DataTable, FormField, Notice, PageHeader, Panel |
| `/admin/cache` | `FileCache` | I | APB | Recently served files | (pinned stats); Files in memory (N) | Notice, PageHeader, Panel, Scroller, StatGrid, StatTile |
| `/admin/settings` | `Settings` | I | - | Settings | Server; Library; Uploads; Previews; File types (c); Subtitle types (c); Recently served files (c); Compatibility (c); Database (c) | CheckField, CollapsiblePanel, ExtensionMapEditor, FormField, Notice, PageHeader, Panel, SubtitleTypeEditor |
| `/admin/settings/temporary` | `TemporarySettings` | I | - | Temporary settings | Show temporarily hidden folders; Show where each value comes from; Television traffic | Notice, PageHeader, Panel |
| `/admin/maintenance` | `Maintenance` | I | APB | Maintenance | Look for changes now; Rebuild details and previews; All metadata read from files; Subscriptions; Rebuild from scratch; Server | ConfirmActionPair, DataTable, MetaList, MetaRow, Notice, PageHeader, Panel, Provenance |
| `/admin/help` | `Help` | S | - | Help | thirteen topic panels, from *What this server is* to *Where things are kept* | PageHeader, Panel |
| `/admin/about` | `About` | S | - | About | This server; What your televisions are told; Copyright; Getting started | DataTable, MetaList, MetaRow, PageHeader, Panel |

(c) = a `CollapsiblePanel`; the others are `Panel`s. Page files live in `src/DlnaServer.Admin/Pages/`.

**Native elements a page draws itself**, outside any shared component - the places a reskin has to style
directly:

| Page | Elements and classes |
|---|---|
| Library | `nav.crumbs`, `a.button`, `.divider` |
| Preview | `video`, `audio`, `img.cover`, `.photo` / `.photozoom` image viewer, `.lightbox.modal[role=dialog]` (`.lightboxbar`, `.lightboxstage`, `.lightboxtitle`, `.zoomlevel`, `.lightboxclose`), `.downloads`, `.tagdump` / `.tagvalue`, `td.num` |
| Photo | `.photopage`, `nav.crumbs`, one `img` |
| SearchFiles | `.searchform`, nine `select`s (`select.multi` for the language lists), `.empty` |
| SearchFolders | `.searchform` |
| Settings | three `textarea`s, `.checks` / `.checkrow` / `.verdict` for the folder checks, `.advanced-heading`, `button.primary` |
| Upload | the one plain `<form method="post" enctype="multipart/form-data">`, radio buttons, `input#files[type=file]`, `.checks`, `.tagdump` |
| FileCache | `Virtualize` over the served paths, `button.danger` |
| Maintenance | `.paths` |
| TemporarySettings | two `input[type=number]` minute boxes and the `input#blockHours` hours box, each inside `.actions` |
| About | `.tagdump` / `.tagvalue` |

## Shared components (`Shared/`, 28)

Files = how many pages and components use it. Required parameters are in **bold**.

| Tag | Description | Parameters | Root markup | Files | Used by |
|---|---|---|---|---|---|
| `<Panel>` | Bordered section with an optional `h2` heading | Heading, HeadingContent, Class (`pinned`, `player`), ChildContent | `div.panel` > `h2` | 14 | About, Dashboard, FileCache, Help, Library, Maintenance, Photo, Preview, Settings, TemporarySettings, Upload, FileList, MediaFileFacts, SubtitleEditor |
| `<PageHeader>` | Page title and standfirst; its `h1` is the navigation focus target | **Title**, ChildContent | `h1` + `p.lede` | 13 | every page |
| `<Notice>` | One-line result message; renders nothing when empty. An action's result goes directly under the buttons that caused it, in their panel; only a failure while the page loads goes at the top | IsBad, Message, ChildContent | `div.notice.ok` / `div.notice.bad` | 12 | Dashboard, FileCache, Library, Maintenance, Preview, Settings, TemporarySettings, Upload, FileTypeEditor, MainLayout, ProcessingActions, SubtitleEditor |
| `<DataTable>` | Table with a header row; caller supplies the `th` cells and the `tr` rows | **Columns**, ChildContent | `table` > `thead` + `tbody` | 7 | About, Maintenance, Preview, Upload, ExtensionMapEditor, SubtitleEditor, SubtitleTypeEditor |
| `<FormField>` | One labelled control in fixed order: label, control, error, hint, restart note | **Label**, For, ChildContent, Error, Hint, Restart | `div.field` > `label` … `.error` | 6 | SearchFiles, SearchFolders, Settings, Upload, FileTypeEditor, SubtitleEditor |
| `<CollapsiblePanel>` | Panel whose body folds away behind a heading button | **Heading**, ChildContent, Footer, Summary, Pinned, StartCollapsed | `div.panel.collapsible[.pinned][.is-collapsed]` > `h2` > `button.collapse-toggle[aria-expanded]` (`.chevron`, `.collapse-summary`) | 5 | Library, Settings, FileTypeEditor, ProcessingActions, SearchPanel |
| `<Provenance>` | Says where a value came from, on hover; inert unless the Temporary-settings period runs | Source, ChildContent | `span.tech[tabindex=0][data-tech]` | 5 | Maintenance, MediaFileFacts, MetaRow, ShowMore, StatTile |
| `<MetaList>` | Definition list of label-and-value facts | ChildContent | `dl.meta` | 4 | About, Maintenance, Preview, MediaFileFacts |
| `<MetaRow>` | One `dt`/`dd` pair inside a MetaList | **Label**, Source, ChildContent | `dt` + `dd` | 4 | About, Maintenance, Preview, MediaFileFacts |
| `<Scroller>` | Box that scrolls its own content instead of the page | ChildContent | `div.scroller[tabindex=-1]` | 3 | FileCache, FileList, FolderList |
| `<CheckField>` | Checkbox with its own text, no heading above it | **Label**, Value, ValueChanged, Hint, Restart, Disabled | `div.field` > `label` > `input[type=checkbox]` | 2 | Library, Settings |
| `<FieldNotes>` | The hint and restart notes under a control | Hint, Restart | `div.hint`, `.restart` | 2 | CheckField, FormField |
| `<FileList>` | Grid of thumbnail cards, optionally scrolling | **Files**, DirectoryId, Scrolling, EmptyMessage | `div.tiles` of `MediaTile` | 2 | Library, SearchFiles |
| `<FolderList>` | Virtualized scrolling list of folder links | **Folders**, ShowPath, EmptyMessage | `ul.folders` (`.paths` when ShowPath) | 2 | Library, SearchFolders |
| `<MediaFileFacts>` | The facts about one media file - kind, size, duration, streams | **File**, Compact, Heading, Actions | `div.muted.facts` / a Panel | 2 | Preview, MediaTile |
| `<ProcessingActions>` | Bulk discard/restore actions over one file or a folder tree, shut by default | **Scope**, Heading, ChildContent, OnCompleted | CollapsiblePanel > `.actions` | 2 | Library, Preview |
| `<SearchPanel>` | Pinned collapsible filter box with Search, Reset and the result count | **Filters**, Summary, HasSearched, IsRunning, ResultCount, Limit, OnSearch, OnReset | CollapsiblePanel > `.actions` > `button.primary` | 2 | SearchFiles, SearchFolders |
| `<StatGrid>` | Grid laying out a row of StatTiles | ChildContent | `div.grid` | 2 | Dashboard, FileCache |
| `<StatTile>` | One labelled figure; `IsOverTarget` colours it as a warning | **Label**, Source, IsOverTarget, ChildContent | `div.stat` > `.label` + `.value` | 2 | Dashboard, FileCache |
| `<ConfirmActionPair>` | Two irreversible actions, each armed by a first click and confirmed or cancelled by a second | **FirstLabel**, **FirstConfirmLabel**, OnFirst, **SecondLabel**, **SecondConfirmLabel**, OnSecond, Disabled, OnArmed | `div.actions` > `button.go` / `button.stop` | 1 | Maintenance |
| `<ExtensionMapEditor>` | The file-type map, one editable row per extension | **Rows** | DataTable rows, `.checkrow`, `button.stop` | 1 | Settings |
| `<FieldPair>` | A from/to pair of filters kept on one grid row | ChildContent | `div.fieldpair` | 1 | SearchFiles |
| `<FileTypeEditor>` | Corrects one file's MIME type and compatibility profile | **File**, OnCompleted | CollapsiblePanel, `button.primary` | 1 | Preview |
| `<MediaTile>` | Thumbnail card for one media file | **File**, **Href** | `a.tile` (`.kindbadge`, `.kindicon`, `.subbadge`, `.noimg`, `.caption`) | 1 | FileList |
| `<NavMenu>` | The sidebar navigation | - | `ul.nav` | 1 | MainLayout |
| `<ShowMore>` | "Showing N of M" and the button fetching the next page; nothing once all are shown | **Shown**, **Total**, **Noun**, Source, Disabled, OnMore | `p.muted` + button | 1 | Library |
| `<SubtitleEditor>` | The subtitle and lyrics files linked to one media file, and how to change them | **File**, OnCompleted | Panel, `.checks` / `.checkrow`, `.verdict`, `.missing` | 1 | Preview |
| `<SubtitleTypeEditor>` | The subtitle types, one editable row per extension | **Rows** | DataTable rows, `.checkrow`, `button.stop` | 1 | Settings |

`MainLayout` is the layout (see *Shell*) and is used by nothing else.

## Non-visual building blocks

| Type | File | What it gives a page |
|---|---|---|
| `AdminPageBase` | `Components/AdminPageBase.cs` | Circuit-lifetime cancellation, the one-call-at-a-time database gate, and `Message` for `Notice`; a database failure becomes a bad message instead of reaching the error boundary |
| `LibraryCountsCache` | `Components/LibraryCountsCache.cs` | The Dashboard's library counts, shared by every open Dashboard and kept until the library changes |
| `IAdminUrls` / `AdminUrls` | `Routing/` | Every admin link a page builds - library folder, preview, photo, the media file, its subtitles, a thumbnail |
| `IMediaFormatter` / `MediaFormatter` | `Formatting/` | Sizes, durations, bitrates, numbers, times, dates and the media kind as the pages print them |
| `BrowserPlayback` | `Playback/BrowserPlayback.cs` | Whether a browser can be expected to present a file, judged by its container - decides the Preview page's player |
| `ExtensionMap`, `SubtitleTypeMap` (+ row types) | `Configuration/` | The editable rows behind the two map editors on Settings |

## What the UI tests hold on to

`tests/DlnaServer.UiTests` drives these pages in headless Chrome against the real Host. It selects on text
and structure, never on a styling class, so a reskin is free to change classes - but **these must survive**:

| Hook | Where | Test |
|---|---|---|
| The page's first `h1` carries the title in the *Pages* table | every page | `AdminPagesTest`, `LibraryPagesTest` |
| `link[rel='stylesheet']` resolves on the admin port | `App.razor` | `AdminPagesTest` |
| Every menu route has a displayed `nav a[href=…]`, and the *Search folders* one opens that page; no `/admin/upload` while uploads are off | `NavMenu` | `AdminPagesTest` |
| Links inside `main` keep their `href` prefix (`/admin/library/`, `/admin/preview/`, `/admin/photo/`) and the folder or file name as their text, and are displayed | `FolderList`, `MediaTile`, Preview | `LibraryPagesTest`, `SearchPagesTest` |
| Folder link text is the folder name as its own text node (`normalize-space(text())`) | `FolderList` | `SearchPagesTest` |
| `a` with the text *Back to folder* and *Download* | Preview | `LibraryPagesTest` |
| `input#name`, `button` *Search* and *Reset* inside `main` | SearchFiles, SearchFolders (`SearchPanel`) | `SearchPagesTest` |
| `input#modelName` and `button` *Save* | Settings | `SettingsPagesTest` |
| `dt` *Model* followed by its `dd` | About (`MetaRow`) | `SettingsPagesTest` |
| A panel's `h2` is a direct child of the panel, so `//h2[…]/..` reaches its buttons | `Panel` | `MaintenancePageTest`, `SettingsPagesTest` |
| *Server* panel: *Restart server* → *Confirm restart* / *Cancel* | Maintenance (`ConfirmActionPair`) | `MaintenancePageTest` |
| *Show temporarily hidden folders* panel: *Show now* / *Hide now* | TemporarySettings | `SettingsPagesTest` |
| *Television traffic* panel: *Block* → *Unblock now*, and a `p` starting *Blocked until* while blocked | TemporarySettings | `SettingsPagesTest` |
| "Uploading is switched off" in `main` and no `input[type='file']` while uploads are off | Upload | `UploadPageTest` |
| No error in the browser console; an interactive page logs `blazor.web.js`'s *WebSocket connected* once its circuit attaches | every page; `App.razor` loads `blazor.web.js` | every fixture's tear-down; `AdminPagesTest`, `LibraryPagesTest` wait for the circuit |

## The stylesheet

`wwwroot/admin.css` is the only stylesheet. Every colour is a token on `:root`, taken from the server's own icons and redefined under `@media (prefers-color-scheme: dark)` - which icon each comes from, and the contrast each pair reaches, is [ADR 0039](ADR/0039-admin-colours-and-shape-come-from-the-device-icon.md). Colour tokens: `--sky`, `--amber`, `--navy` (the icons' own colours), `--ink`, `--muted`, `--line` (hairlines), `--edge` (control borders, 3:1), `--panel`, `--ground`, `--accent`, `--on-accent`, `--band` / `--band-ink` (the current menu item), `--warn`, `--go`, `--stop`. Shape tokens: `--radius` (cards and tiles, 12px), `--radius-control` (buttons, inputs, menu items, 8px), `--shadow` (the one card shadow). A new colour comes from the icons and is added to the ADR's table. `:focus-visible` draws one accent outline everywhere, and the hover colour transitions run only under `prefers-reduced-motion: no-preference`. `button.danger` is outlined in `--warn` at rest and tinted on hover; the three filled buttons (`primary`, `go`, `stop`) darken by `filter: brightness(.9)` on hover. A provenance tooltip (`.tech::after`) is anchored under its value with CSS anchor positioning and flips to stay on screen; without anchor positioning it is a bar at the foot of the screen. On a phone a `.tagdump` table stacks each row's cells. **A modal overlay carries `.modal`**: while one is on the page, `.layout:has(.modal)` hides everything else (`visibility`, which the overlay turns back on for itself), so Tab and a screen reader stay inside the overlay - no JavaScript, nothing for the page to undo. The photo viewer uses it; a new modal adds the class, and must not need the page visible behind it.

Breakpoints: `max-width: 760px` is the phone layout (sidebar becomes a top bar, `.sitefoot` hidden, menu behind `#menutoggle`), `min-width: 761px` the wide one, and `max-width: 1200px` narrows the statistics grids. `Virtualize` positions rows by a fixed `ItemSize`, so the row heights `FolderList` and FileCache pass must match what the stylesheet draws - `.folders a` (padding, border, the body's 15px/1.5) and `th, td` padding with `.paths` decide them; measured after the restyle at 41.5 (Library folder) and 59.5 (folder search), unchanged. FileCache's served-paths row is 33 at every width, because `.scroller table.paths td` pins its padding and line height against the phone layout's narrower `th, td`.
