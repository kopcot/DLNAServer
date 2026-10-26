# 0009. `IsSourceRoot`'s index is kept

- **Status:** Accepted
- **Date:** Not recorded - from the round-2 fact sheet, before 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 9

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

From the round-2 fact sheet, decided by the maintainer with reasons.

## Decision

**`IsSourceRoot`'s index is kept** - it is used, as the filter in `GetSourceRootsAsync`.

## Consequences

Reporting it as an unused index is a false positive.
