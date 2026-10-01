# Security Policy

## Supported versions

Only the latest release receives fixes. The version scheme is `Major.Minor.MonthDate`. The release
version is the `<Version>` in `src/DlnaServer.Host/DlnaServer.Host.csproj`; every other project carries
its own and is bumped only when it changes.

| Version | Supported |
| ------- | --------- |
| `1.1.x` | yes       |
| `< 1.1` | no        |

## Verifying a release

Every release is built, tested and smoke tested on GitHub Actions from the tagged commit, never on a
workstation; [`workflows/README.md`](workflows/README.md) describes each step. The container image is signed
keyless with cosign, so a pulled image can be checked against the workflow that built it:

```bash
cosign verify ghcr.io/kopcot/dlnaserver:v<version> \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com \
  --certificate-identity-regexp '^https://github\.com/kopcot/DLNAServer/\.github/workflows/docker-publish\.yml@'
```

The same pipeline runs CodeQL on the code, scans the image for known vulnerabilities, and checks the NuGet
packages against the advisory database every night.

## Reporting a vulnerability

Report privately through GitHub: **Security → Advisories → Report a vulnerability** on this
repository. Do not open a public issue for a security problem.

Please include the version (the admin *About* page reports it), how the server is exposed on your
network, and the smallest set of steps that reproduces the problem. Expect an acknowledgement within
a week. If the report is accepted, the fix ships in the next release and the advisory is published
once it is available; if it is declined, you will get the reasoning.

## Design decisions that are not vulnerabilities

The following are deliberate and documented, so a report about them will be closed as working as
intended. They are listed here so the boundary is explicit.

- **The server has no authentication.** It is built for a trusted home LAN, and DLNA itself has no
  concept of a user. Anything that can reach the media port can browse and stream the library.
- **The admin UI is unauthenticated for the same reason**, and it can stop the server, rebuild the
  index and delete the database. Exposing it beyond the LAN is only supported behind the reverse
  proxy and password in `deploy/docker-compose/2-admin-remote`; see `Docker.usage.md`.
- **Uploading is off by default** and is the only path that writes into the media tree. It is read
  once at startup, so enabling it needs a restart. Every upload is recorded in
  `logs/uploadSecurity.log`.
- **`Thumbnails.DownloadFFmpeg` is off by default** because it fetches and executes a third-party
  binary. Turning it on is a deliberate choice.

A report that one of these can be reached from outside the network it was configured for, or that a
guard on one of them can be bypassed, *is* in scope and worth sending.
