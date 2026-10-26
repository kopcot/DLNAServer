# 0003. The media byte-cache machinery is inert, not dead

- **Status:** Accepted
- **Date:** Not recorded - from the round-2 fact sheet, before 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 3

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

From the round-2 fact sheet, decided by the maintainer with reasons. The review recommended deleting the machinery.

## Decision

**The media byte-cache machinery is deliberately INERT, not dead.** `MediaCacheBacklog`, `MediaCacheFillHostedService` and `CachedContentClass`'s Media arm are kept although the shipped 32 MB per-file limit admits no film. Chosen over the review's recommendation to delete them.

## Consequences

Reporting them as dead code is a false positive. See also [0012](0012-whole-file-buffering-survives.md).
