# 0028. Accepted limit - delivery checks the path, then opens it again

- **Status:** Accepted
- **Date:** 2026-09-26
- **Formerly:** `docs/decisions.md` section 7b, standing decision 28

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Maintainer ruling 2026-09-26, [history.md](../history.md) section 6t.

## Decision

**Accepted limit: subtitle and media delivery check the path, then open it again.** `SubtitlePath.TryLocate` refuses a symlinked file and a symlinked sub-folder, but `PhysicalFile` opens the path afresh. So a file swapped for a link between the two is still served. A FIFO, socket or hard link on the share also passes, and `GetFile` has no link check at all. Every one of these needs write access to the media share, which on this deployment is already full control of the library.

## Consequences

Closing it would need an open-then-verify helper, for example reading `/proc/self/fd` on Linux, shared by both endpoints. **Reporting this as a new finding is a false positive** unless the threat model changes.
