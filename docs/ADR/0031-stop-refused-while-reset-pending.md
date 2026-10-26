# 0031. Stop is refused while a database reset is pending

- **Status:** Accepted
- **Date:** 2026-09-26
- **Formerly:** `docs/decisions.md` section 7b, standing decision 31

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

[history.md](../history.md) section 6u.

## Decision

**Stop is refused while a database reset is pending.** `/manage/stop` answers 409 and the page says why.

## Consequences

Clearing the restart flag would lose the reset; keeping it would turn Stop into a restart.
