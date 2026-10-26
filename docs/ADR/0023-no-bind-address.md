# 0023. `ServerOptions` has no bind address

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 23

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, during the fix pass for the third `/review-all --full` ([history.md](../history.md) section 6i). Considered as the fix for [0022](0022-manage-on-the-media-port.md).

## Decision

**`ServerOptions` has no bind address, and is not getting one.** Considered and rejected: binding one LAN address breaks discovery on a multi-homed box, and it adds a setting the validator and the Settings page both have to learn.

## Consequences

The endpoint filter costs neither.
