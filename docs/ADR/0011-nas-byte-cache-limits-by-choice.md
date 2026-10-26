# 0011. The NAS byte cache stays at 5120 / 512, by choice

- **Status:** Accepted
- **Date:** Not recorded - from the round-2 fact sheet, before 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 11

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

From the round-2 fact sheet, decided by the maintainer with reasons.

## Decision

**The byte cache on this NAS stays at 5120 / 512, by choice.** 512 MB per file re-admits films, which is the 5,073 MB mechanism - that is understood and intended *there*, while the shipped default stays 256 / 32.

## Consequences

It has been "corrected" twice already, both times wrongly. Do not raise it as drift. Its sibling is [0014](0014-nas-database-memory-settings-by-choice.md).
