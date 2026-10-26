# 0015. `transferMode.dlna.org` follows the flags

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 15

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, while closing the last of the review documents deleted that day.

## Decision

**`transferMode.dlna.org` follows the flags, not the other way round.** `ContentFeaturesFor` sends the reference's `Image ? Interactive : Streaming` and **must not be narrowed** - `BrowseItemMapper.GetResourceProtocolInfo` asks exactly that one question, and these values are matched literally by televisions validated against them. It was `ResolveTransferMode` that moved, so `Subtitle` and `Unknown` now echo `Streaming`, which is what their own flags always claimed.

## Consequences

Both sides read `DlnaProtocolInfo.IsStreamed` now, so they cannot disagree again.
