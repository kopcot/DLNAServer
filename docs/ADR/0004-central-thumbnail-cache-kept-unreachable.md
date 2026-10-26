# 0004. `Thumbnails.CacheDirectory` and the central-cache branch are kept

- **Status:** Accepted
- **Date:** Not recorded - from the round-2 fact sheet, before 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 4

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

From the round-2 fact sheet, decided by the maintainer with reasons.

## Decision

**`Thumbnails.CacheDirectory` and the central-cache branch are unreachable and kept**, marked as such.

## Consequences

`Thumbnails.SubFolderName` is required, which is what makes them unreachable (see `CLAUDE.md`). Reporting them as dead code is a false positive.
