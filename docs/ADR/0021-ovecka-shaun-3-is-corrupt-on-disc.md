# 0021. `ovecka.shaun.3.avi` is corrupt on disc

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 21

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, while closing the last of the review documents deleted that day.

## Decision

**`ovecka.shaun.3.avi` is corrupt on disc, not mishandled.** 733 MB, and its first bytes are `d8ddb2ce72e5718c` - no known container signature. ffprobe reported `608x352 mpeg4` but `duration 00:00:00` and `bitrate 0`. Its sibling `ovecka.shaun.4.avi` is already in the "24 previews that have permanently given up" table ([decisions.md](../decisions.md) section 7).

## Consequences

Nothing to fix in the server.
