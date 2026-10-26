# 0014. The NAS database memory settings are the operator's choice

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 14

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, while closing the last of the review documents deleted that day. Section 4 credits `mmap 128 -> 0` and `cache 32 -> 2` with the 563.4 -> 177.7 MB drop, so the live values read like drift.

## Decision

**The live NAS `config.json` runs `Database.MemoryMapLimitInMegabytes: 64` and `CacheSizeInMegabytes: 8`, by the operator's choice**, where the tree ships `0` and `2`. It reads like drift and is not.

## Consequences

The sibling of [0011](0011-nas-byte-cache-limits-by-choice.md), and the same instruction: do not raise it, and do not "correct" the live file.
