# 0007. First-fill index dating is not gated on `UseFileCreationDateTime`

- **Status:** Accepted
- **Date:** Not recorded - from the round-2 fact sheet, before 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 7

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

From the round-2 fact sheet, decided by the maintainer with reasons.

## Decision

**The first-fill index-date rule is deliberately not gated on `UseFileCreationDateTime`.**

## Consequences

The rule itself - a first fill dates rows from the filesystem, and only a first fill - is described in `CLAUDE.md`. Reporting the missing gate as a defect is a false positive.
