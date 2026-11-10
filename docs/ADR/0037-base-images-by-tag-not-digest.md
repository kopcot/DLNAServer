# 0037. The Dockerfile names its base images by tag, not by digest

- **Status:** Accepted
- **Date:** 2026-11-10

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

`e8a5b9d` (2026-10-01) pinned `mcr.microsoft.com/dotnet/sdk:8.0` and `mcr.microsoft.com/dotnet/aspnet:8.0` by the digest of each tag's multi-arch index, to close OpenSSF Scorecard's *Pinned-Dependencies* alerts #8 and #9. Dependabot's docker entry was meant to move the digest whenever Microsoft rebuilt the tag. The decision was recorded only in that commit message.

It failed on 2026-11-04. Microsoft published .NET 8.0.33 with the fix for GHSA-p3jp-9v83-76w9 (CVE-2026-111497, a HIGH out-of-bounds write in the Brotli decoder), the image stayed on 8.0.32, and the nightly `Full test suite` went red six nights running on the Trivy scan, on an unchanged commit. Nothing would have moved it: in the 40 days the images were pinned, Dependabot's docker entry (weekly, 7-day cooldown) opened no digest pull request at all - both digest moves were made by hand, in `e8a5b9d` and `cd5f3fe` - and GitHub never raises a security update for a base image. The catalogue entry is [0035](0035-ci-cd-failures-and-their-fixes.md), entry 21.

The pin bought one thing: a rebuild of one commit produced the same image. For this server that is worth less than the runtime patch. The image is rebuilt on every release and by the operator on the NAS, and in both cases the wanted result is the current 8.0 patch, not the one the commit happened to record.

## Decision

**Both base images are named by tag only - `sdk:8.0` and `aspnet:8.0`, no `@sha256:` digest.** Decided by the maintainer on 2026-11-10, reversing the pin from `e8a5b9d`.

- The Dependabot docker entry is removed. With no digest and major versions out of the question (`global.json` pins the 8.0.4xx SDK and the solution targets `net8.0`), it had nothing left to propose.
- The compose files that build the image set `pull: true` under `build:`, so a rebuild on the NAS fetches the current 8.0 tag instead of reusing a cached copy.
- The CI builds (`docker.yml`, `docker-publish.yml`) are left as they are: each runs on a fresh runner through `docker/setup-buildx-action`'s builder, which has no local copy of the base image and resolves the tag from the registry on every build. `docker.yml`'s GitHub Actions layer cache cannot hold an old runtime base back either, because its `runtime` stage is never served from cache ([0035](0035-ci-cd-failures-and-their-fixes.md), entry 14).

## Consequences

- **Scorecard's *Pinned-Dependencies* check reports the two `FROM` lines again** (the alerts `e8a5b9d` closed). That is expected; dismiss them with a pointer here rather than re-pinning.
- **A build is not reproducible by commit.** Two builds of one commit a week apart can carry different .NET and Debian patches. A release is still identified exactly: `docker-publish.yml` signs and smoke tests the pushed image by its own digest.
- **An unpinned `docker build` on a machine with a cached base image reuses that copy.** Pass `--pull`; the compose files do it for you through `pull: true`, so `docker compose up --build` as `Docker.usage.md` writes it is enough.
- Trivy in `docker.yml` stays the detector. A HIGH or CRITICAL with a fix now clears on the next build after Microsoft ships it, with no commit needed - unless the fix is in a .NET major the solution does not target.
- NuGet packages stay pinned by `packages.lock.json` and actions by SHA; this decision covers only the two base images.
