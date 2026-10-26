# 0033. The smoke test does not exercise SSDP discovery

- **Status:** Accepted
- **Date:** 2026-09-30
- **Formerly:** `docs/decisions.md` section 7b, standing decision 33

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

[history.md](../history.md) section 6x.

## Decision

**The smoke test does not exercise SSDP discovery.** A runner has no television on its network, so it checks the HTTP half only.

## Consequences

**Reporting the missing discovery test as a gap is a false positive.**
