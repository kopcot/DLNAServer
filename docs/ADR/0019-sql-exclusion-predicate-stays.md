# 0019. The SQL exclusion predicate stays as it is

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 19

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, while closing the last of the review documents deleted that day. Raised as W10.

## Decision

**The SQL exclusion predicate stays as it is** (W10). Its per-row `replace()` and two concats were never measured as a cost, and the proposed replacement adds a subquery to the browse path where `ExcludeWithoutVisibleMedia` depends on a covering-index range seek. Evidence for leaving it: the NAS browse baseline is p50 7.6 ms / p99 31.4 ms, `SlowQueryInterceptor` is registered at a 50 ms threshold, and a full day of live logs over 25,532 files holds **zero** "Slow database command" entries.

## Consequences

Re-open it only with a measurement that contradicts that. (The label W10 has since been reused for the subtitle-sync measurement in [decisions.md](../decisions.md) section 7b.)
