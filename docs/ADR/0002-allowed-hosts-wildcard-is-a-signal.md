# 0002. `AllowedHosts: *` is a signal, not a wildcard

- **Status:** Accepted
- **Date:** Not recorded - from the round-2 fact sheet, before 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 2

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

From the round-2 fact sheet, decided by the maintainer with reasons.

## Decision

**`appsettings.json` reads `"AllowedHosts": "*"` on purpose.** That value is the SIGNAL that makes `AllowedHostsDefaults` generate the real allowlist at startup.

## Consequences

It is not an unfixed wildcard. Reporting it as one is a false positive.
