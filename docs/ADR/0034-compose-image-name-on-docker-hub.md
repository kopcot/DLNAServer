# 0034. Compose files name the image `kopcot/dlnaserver:latest`

- **Status:** Accepted
- **Date:** 2026-10-26

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

The compose files under `deploy/docker-compose/` (`1-QNAP-TS464-40GB` and `2-admin-remote`) carry `image: kopcot/dlnaserver:latest` beside a `build:` block, while releases publish to `ghcr.io/kopcot/dlnaserver` ([.github/workflows/README.md](../../.github/workflows/README.md)). A reviewer reading the two side by side reports the compose value as a wrong or missing registry. Decided by the maintainer on 2026-10-26.

## Decision

**The compose files use `image: kopcot/dlnaserver:latest`, a Docker Hub name, deliberately.** The maintainer owns the `kopcot` namespace on Docker Hub.

## Consequences

**Reporting it as "should be `ghcr.io/kopcot/dlnaserver`" is a false positive.** With the `build:` block in place compose builds from GitHub's `main` and only names the result; `Docker.usage.md` explains that, and how to switch a deployment to the published GHCR image instead.
