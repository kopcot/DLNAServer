# 0025. The `DLNA.ORG_OP` flag-bit naming is correct

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 25

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, during the fix pass for the third `/review-all --full` ([history.md](../history.md) section 6i). A review reported the bits as inverted. Raised by a reviewer running without semantic analysis; verified against the emitted value before it was rejected.

## Decision

**The `DLNA.ORG_OP` flag-bit naming is CORRECT - a review reported it as inverted and was wrong.** `Format` renders `((short)operation).ToString("B2")` and the wire form is `<byte-seek><time-seek>`, so `TimeSeekSupported = 1 << 0` renders `01` (time-seek, correct) and `ByteSeekSupported = 1 << 1` renders `10` (byte-seek, correct).

## Consequences

**Renaming them would introduce the inversion the finding claimed to fix.** The trap this taught is in [decisions.md](../decisions.md) section 7b, *Traps from the 2026-09-08 fix pass*.
