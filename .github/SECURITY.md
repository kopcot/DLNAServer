# Security Policy

## Supported versions

Only the latest release receives fixes. The version scheme is `Major.Minor.MonthDate`, carried by a
single `<Version>` property in `Directory.Build.props`.

| Version | Supported |
| ------- | --------- |
| `1.1.x` | yes       |
| `< 1.1` | no        |

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
  proxy and password in `docker-compose.admin-remote.yml`; see `Docker.usage.md`.
- **Uploading is off by default** and is the only path that writes into the media tree. It is read
  once at startup, so enabling it needs a restart. Every upload is recorded in
  `logs/uploadSecurity.log`.
- **`Thumbnails.DownloadFFmpeg` is off by default** because it fetches and executes a third-party
  binary. Turning it on is a deliberate choice.

A report that one of these can be reached from outside the network it was configured for, or that a
guard on one of them can be bypassed, *is* in scope and worth sending.
