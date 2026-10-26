# 0017. `IsUnderFirstFillRoot` compares ordinally

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 17

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, while closing the last of the review documents deleted that day. The dedup pass reported the ordinal comparison as a defect.

## Decision

**`IsUnderFirstFillRoot` comparing `StringComparison.Ordinal` is CORRECT.** Source folders are compared ordinally on purpose in `NormaliseSourceFolders` and `IsInsideAnySourceFolder`, and `MediaDirectoryEntityConfiguration` makes `FullPath` case-sensitive for the same reason - production is Linux, where two paths differing only in case are two directories.

## Consequences

The case-insensitive rule belongs to *exclusion entries*, which are a different thing. Reporting the ordinal comparison as a defect is a false positive.
