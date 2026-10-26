# 0026. A file moved into an excluded folder has its row removed

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 26

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, during the fix pass for the third `/review-all --full` ([history.md](../history.md) section 6i).

## Decision

**A file moved INTO an excluded folder has its row REMOVED, and that is not the same rule as decision-by-configuration hiding.** When a folder already indexed becomes excluded by a config change its rows survive, so un-hiding is free - `IndexAsync_LeavesAnExcludedFoldersRowsInPlace` pins that. When a file *moves* into an already-excluded folder the destination is never enumerated, so nothing can re-point the row and reconciliation finds the old path definitely absent. Removing it is right: the operator deleted the file.

## Consequences

A consequence worth knowing is that a round trip out again produces a **new `PublicId`**, unlike a move within the library. Three tests in `LibraryIndexerTest` cover the three cases.
