# 0039. The admin UI takes its colours and shapes from the server's own icon

- **Status:** Accepted
- **Date:** 2026-11-10

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

The admin UI was restyled for version 2.0. The maintainer chose the direction - "clean modern": neutral greys, one accent colour, soft cards with a subtle shadow, generous spacing, the system font, a light and a dark theme - and set one condition on it: the restyle should resemble the server's icons, "so user can know, that icon and application are the same from the first look". The icon in question is the one a television shows in its source list, which is also the browser tab icon, so it is the first picture of this server most people see.

`admin.css` already sampled one colour from that icon, but used it only for a 10px square beside the name. This record writes down what is taken from the icons, where it goes, and the rule that keeps the two from drifting apart.

## The source icons

All under `src/DlnaServer.Host/Resources/images/icons/`, copied byte for byte from the reference server and advertised in `description.xml`'s `iconList`. They are not edited by this decision.

| File | Size | What it is |
| --- | --- | --- |
| `small.png` | 48 x 48 | The device icon. Also the browser tab icon (`/favicon.ico` and `/admin/favicon.ico`) |
| `large.png` | 120 x 120 | The device icon. Shown in the admin sidebar beside the name, through `/admin/icon/large.png` |
| `extraLarge.png` | 512 x 512 | The device icon, the size the shape language below is read from |
| `small.jpg`, `large.jpg`, `extraLarge.jpg` | as above | JPEG copies of the same three |
| `file{Movie,Audio,Image}.jpg`, `folder*.jpg` | 342 x 342 | The media-kind icons: shown on a library tile that has no preview, and sent to televisions as fallback thumbnails |

**The device icon** is a flat drawing of a stacked server on a light-blue field: white and light-blue horizontal bands, a thin, darker blue edge where a band meets the light, a cool slate-grey side face, small rounded rectangles for the ports and lights, and one soft shadow cast down to the right. Measured by counting pixels in `large.png`, the field and the bands are `#b9e1fa`, the white is `#ffffff` with a warm `#f9f8f6`, the edge sits between `#94cdec` and `#a5d4f2` (recorded as `#9cd4f9` when the admin UI was first built), and median-cut quantisation of `extraLarge.png` puts the side face at `#77a0ad`.

**The media-kind icons** are a second family: amber `#fec409` shapes outlined in a dark navy, with cream `#f7e8a1` fills. Median-cut quantisation puts the outline at `#112b35`; the individual dark pixels range around it (`#0f3041` to `#132f38`), and `#112b35` is the value the tokens use.

## Decision

**Every colour in `admin.css` comes from these icons.** The light pastels cannot carry text on white - `#b9e1fa` on white is 1.4:1 - so where a colour has to be read it stays in the icon colour's hue family - within a few degrees - and is darkened (or, in the dark theme, lightened) until it passes WCAG AA. The tokens on `:root`, light and dark:

| Token | Light | Dark | Taken from |
| --- | --- | --- | --- |
| `--sky` | `#b9e1fa` | `#b9e1fa` | The device icon's field and bands, as measured |
| `--amber` | `#fec409` | `#fec409` | The media-kind icons' fill |
| `--ink` (text) | `#112b35` | `#e3edf1` | The media-kind icons' outline; in the dark, a near-white on the same hue |
| `--muted` (secondary text) | `#52707a` | `#93abb5` | The slate side face, darkened (light) or lightened (dark) to pass AA |
| `--ground` (page) | `#f4f7f8` | `#0f1619` | Slate at 8% over white; in the dark, a near-black on the slate hue |
| `--panel` (cards) | `#ffffff` | `#172024` | The icon's white; in the dark, one step above the ground |
| `--line` (hairlines) | `#e1eaed` | `#28343a` | Slate at 22% over white; one step above the panel in the dark |
| `--edge` (control borders) | `#638e9c` | `#58757e` | The slate side face, at the 3:1 that WCAG 1.4.11 asks of a control's boundary |
| `--band`, `--band-ink` (the current page in the menu) | `--sky` behind `--ink` | `--sky` at 16% over the panel, behind `--sky` | The device icon's bands |
| `--accent` (links, the primary button, focus rings) | `#1c6b9c` | `#b9e1fa` | The sky hue (203°) at 36% lightness; in the dark, the icon's own sky, unchanged |
| `--on-accent` (text on the accent) | `#ffffff` | `#112b35` | White; in the dark, the navy outline |
| `--warn` | `#8f6400` | `#fec409` | The amber family (42° against the icon's 46°), deepened to pass AA on white; in the dark, the icon's own amber |
| `--go`, `--stop` | `#1f7a3d`, `#b3261e` | the same | **Not from the icons** - the green and red of a confirm-or-back-out pair, kept from before because a decision button must read as go or stop, not as the brand |

The current page in the menu is a band of `--sky` in the light theme and of `--sky` at 16% over the panel in the dark - the icon's stripes, used where the eye looks for "you are here".

**The shapes come from the device icon too.** Cards have a 12px radius (10px on a phone) and controls 8px - the icon's slightly softened corners, not pills and not sharp boxes. The kind badges on tiles stay fully round, like the icon's lights. Borders are 1px hairlines, like the icon's edges. A card carries one soft shadow, offset downward and tinted with the navy rather than grey, which is the icon's cast shadow. There are no gradients: the icon is flat. The sidebar shows `large.png` itself beside the name, so the picture and the page sit side by side.

**The rule:** a new colour in the admin UI is taken from these icons - a token above, or the hue of one with its lightness moved for contrast - and is added to this table. If the device icon or the media-kind icons change, the sampled values above are measured again and the tokens follow in the same change. The exceptions are functional, not brand colours: `--go` and `--stop`, and the black behind a video and the photo viewer, which is a letterbox rather than a colour.

## Contrast, WCAG 2.2

Measured with the WCAG relative-luminance formula on the token values above. AA asks 4.5:1 for text and 3:1 for a control's boundary.

| Pair | Light | Dark |
| --- | --- | --- |
| Text (`--ink`) on a card | 14.78 | 13.91 |
| Text on the page ground | 13.73 | 15.36 |
| Secondary text (`--muted`) on a card | 5.31 | 6.88 |
| Secondary text on the page ground | 4.93 | 7.60 |
| Link / accent on a card (every link in running text takes the accent) | 5.78 | 11.99 |
| Link / accent on the page ground | 5.37 | 13.24 |
| Primary button text (`--on-accent` on `--accent`) | 5.78 | 10.71 |
| Current menu item text on its band | 10.71 | 7.88 |
| Warning text on a card | 5.26 | 10.34 |
| Warning text on the page ground | 4.89 | 11.41 |
| Success notice text on its tinted background | 5.17 | 9.92 |
| Failure notice text on its tinted background | 4.75 | 8.79 |
| White on `--go` / `--stop` | 5.37 / 6.54 | 5.37 / 6.54 |
| Control border (`--edge`) on a card | 3.57 | 3.36 |
| Control border on the page ground | 3.32 | 3.71 |

## Consequences

- The admin UI and the icon a television shows read as one product, in both themes. The dark theme is where the icon's own sky and amber appear unchanged, because on a dark ground they are the readable ones.
- `--sky` and `--amber` themselves are never used for text on a light ground: they fail contrast there. That is why the light theme reads text in the darkened versions and the pastels appear only as fills.
- A colour picked because it looks right, without coming from the icons, is a defect against this record. So is changing an icon without re-measuring the tokens.
- Form controls and outlined buttons are drawn with `--edge` rather than `--line`, so their boundary passes 3:1 - the hairline `--line` stays for dividers and card edges, which are not controls.
- The icons stay byte-identical to the reference's, so televisions see nothing new. This decision changes the admin pages only.
- The tests in `tests/DlnaServer.UiTests` select by tag, id, link address and text, never by class ([ADR 0038](0038-admin-ui-tests-drive-the-real-host.md)), so they stay valid across a restyle that keeps the pages working.
