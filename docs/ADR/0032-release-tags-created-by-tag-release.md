# 0032. Release tags are created by `tag-release.yml`

- **Status:** Accepted
- **Date:** 2026-09-30, revised 2026-10-01
- **Formerly:** `docs/decisions.md` section 7b, standing decision 32

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

[history.md](../history.md) section 6y. The failure that forced the 2026-10-01 revision is entry 13 of [0035](0035-ci-cd-failures-and-their-fixes.md).

## Decision

**Release tags are created by `tag-release.yml`, never by hand on a workstation.** Since 2026-10-01 it tags the pushed head with its version, not the version left behind - the workflow token cannot tag an older commit once a workflow file has changed after it, which failed `v1.1.0928`.

## Consequences

A missing local `git tag` step in a change is therefore not an omission. The `./` reusable-workflow calls with a zizmor `self-repository` ignore are deliberate too, until actionlint accepts the `$/` form. The whole flow is in [.github/workflows/README.md](../../.github/workflows/README.md).
