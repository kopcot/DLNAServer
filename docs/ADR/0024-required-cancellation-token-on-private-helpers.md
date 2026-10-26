# 0024. Private and internal `async` helpers take a required `CancellationToken`

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 24

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, during the fix pass for the third `/review-all --full` ([history.md](../history.md) section 6i).

## Decision

**A private or internal `async` helper here takes a REQUIRED `CancellationToken`.** See [decisions.md](../decisions.md) section 7.

## Consequences

Roughly forty of them; every one is already called with a real token. Reporting the missing `= default` as a defect is a false positive.
