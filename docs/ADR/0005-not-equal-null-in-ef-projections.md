# 0005. `!= null` inside an EF projection is correct

- **Status:** Accepted
- **Date:** Not recorded - from the round-2 fact sheet, before 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 5

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

From the round-2 fact sheet, decided by the maintainer with reasons.

## Decision

**`!= null` inside an EF projection or predicate is CORRECT** - `is null` is a compile error there.

## Consequences

Do not "modernise" these to `is null` / `is not null`; the code would not compile.
