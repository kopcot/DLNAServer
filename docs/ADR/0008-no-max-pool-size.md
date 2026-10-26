# 0008. `Max Pool Size` was tried and reverted

- **Status:** Accepted
- **Date:** Not recorded - from the round-2 fact sheet, before 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 8

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

From the round-2 fact sheet, decided by the maintainer with reasons.

## Decision

**`Max Pool Size` was tried and reverted** - not a valid `Microsoft.Data.Sqlite` keyword (trap 12).

## Consequences

The trap is kept in [decisions.md](../decisions.md) section 7b, *Traps from the round-1 fix pass*: the server refuses to start while the build and every test stay green. `Database.CacheSizeInMegabytes` is the only lever over the total page cache.
