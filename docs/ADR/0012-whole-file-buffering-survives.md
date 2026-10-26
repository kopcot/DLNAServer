# 0012. Whole-file buffering in the byte cache survives

- **Status:** Accepted
- **Date:** 2026-09-03
- **Formerly:** `docs/decisions.md` section 7b, standing decision 12

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-03. It was previously recorded in a section of the round-1 record that had already been lost. The open question was whether whole-file buffering should survive at all; the review wanted the machinery deleted.

## Decision

**Whole-file buffering in the byte cache survives, with shipped limits that admit no film.**

## Consequences

See [0003](0003-media-byte-cache-machinery-is-inert.md).
