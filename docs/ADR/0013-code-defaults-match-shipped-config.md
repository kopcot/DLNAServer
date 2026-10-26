# 0013. The code defaults match the shipped `config.json`

- **Status:** Accepted
- **Date:** 2026-09-03
- **Formerly:** `docs/decisions.md` section 7b, standing decision 13

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-03. It was previously recorded in a section of the round-1 record that had already been lost. The code defaults were 1024 / 512 against a shipped 256 / 32, so every deployment arriving without a config file silently ran the configuration measured at 5,073 MB.

## Decision

**The code defaults match the shipped `config.json`.**

## Consequences

A deployment without a config file now runs the same limits as one with the shipped file.
