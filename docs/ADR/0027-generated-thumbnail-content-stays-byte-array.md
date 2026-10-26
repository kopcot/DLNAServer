# 0027. `GeneratedThumbnail.Content` stays a `byte[]`

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 27

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, during the fix pass for the third `/review-all --full` ([history.md](../history.md) section 6i). The array check in `ContractImmutabilityTest` was added because init-only stops the reference being replaced and not the contents.

## Decision

**`GeneratedThumbnail.Content` stays a `byte[]`, exempted by name in `ContractImmutabilityTest`.** This one instance is allowed because moving the contract to `ReadOnlyMemory<byte>` would add a full copy of every generated thumbnail at the persistence boundary, where EF wants an array - in the one project whose hard constraint is memory.

## Consequences

A **new** array on a DTO still fails the test.
