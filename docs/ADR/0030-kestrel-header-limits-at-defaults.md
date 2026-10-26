# 0030. Kestrel's request-header limits stay at their defaults

- **Status:** Accepted
- **Date:** 2026-09-26
- **Formerly:** `docs/decisions.md` section 7b, standing decision 30

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

[history.md](../history.md) section 6u.

## Decision

**Kestrel's request-header limits stay at their defaults.** They already bound a header flood; a tighter limit turns a dev box's `localhost` cookie jar, which browsers do not scope by port, into 431s.

## Consequences

**Reporting the defaults as a finding is a false positive.**
