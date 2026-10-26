# 0018. `IMediaFileRepository.AddRangeAsync` is kept

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 18

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, while closing the last of the review documents deleted that day. Raised as W6: no production path calls it.

## Decision

**`IMediaFileRepository.AddRangeAsync` is kept although no production path calls it** (W6). It is how a test seeds rows and then reads back the identifiers the database assigned, which around thirty fixtures rely on; retiring it buys nothing at runtime and costs a rewrite of all of them. Its doc says so now.

## Consequences

Anything that ships uses `AddRangeReturningCountAsync`.
