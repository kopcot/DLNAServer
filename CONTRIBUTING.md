# Contributing

## Getting it running

The solution is `net8.0`, pinned by `global.json` to the 8.0.4xx SDK band. Nothing else is needed to
build; ffmpeg is optional and only affects video and audio metadata.

```powershell
dotnet build DlnaServer.sln          # must stay at 0 warnings
dotnet test  DlnaServer.sln
dotnet run --project src\DlnaServer.Host    # needs Library.SourceFolders set in config.json
```

`Docker.usage.md` covers the container path and `NasBuild.usage.txt` the QNAP deployment.

## The two hard rules

- **The build stays at 0 warnings.** CI builds with `-warnaserror`, so a warning is a failed build.
- **No package carries a known advisory.** `NuGetAudit` runs in `all` mode, transitive packages
  included. If an advisory appears in a package nothing here references directly, pin it in
  `Directory.Packages.props` the way `System.Security.Cryptography.Xml` is pinned, and say why.

## Conventions

`CLAUDE.md` is the working description of the architecture and the conventions the code follows -
block-scoped namespaces, one type per file, `[LoggerMessage]` logging, entities that never leave the
persistence assembly, reads projected straight into DTOs. Read it before the first change; the
architecture tests in `tests/DlnaServer.ArchitectureTests` fail the build if the layering is broken,
so several of those conventions are enforced rather than merely documented.

`PLAN.md` is the decision record. If a change comes out of a decision, or if it cost time because of
a trap worth remembering, that is where it goes.

## Tests

Add or update tests with the change. The three projects split by what they need:

| Project | For |
| --- | --- |
| `DlnaServer.UnitTests` | a type in isolation |
| `DlnaServer.IntegrationTests` | real components wired through DI - no host, no `WebApplicationFactory` |
| `DlnaServer.ArchitectureTests` | layering and the entity/DTO boundary |

**A green build is not verification for a configuration-shaped change.** MSBuild properties,
`runtimeconfig` entries, middleware and registration order have almost no test coverage - a wrong one
compiles clean and every test still passes. Verify those by reading the generated artifact or by
running the server.

Anything that changes what goes on the wire needs a real renderer. VLC tolerates malformed DIDL-Lite
that televisions reject, so VLC alone does not settle it.

## Versioning and releases

Each project under `src/` carries its own `<Version>`, in the form `Major.Minor.MonthDate`. Bump the
ones your change actually touched, and leave the rest alone - that is the whole point of the number.
`DlnaServer.Host` is the product version: it is what the release tag names and what the About page
shows. Test projects are not versioned individually and take the product version from
`Directory.Build.props`.

If an operator would notice the change, add an entry to `release-notes.md` in their terms - it is not
a build log.

A release is a `v`-prefixed tag on the host's version (`v1.1.0923`); pushing it builds, tests and
attaches the linux-x64 archive.

## Pull requests

Say what changed, why, and how you verified it - the template asks for exactly that. Keep the diff to
the change: unrelated reformatting and drive-by refactors make the real edit hard to find.

## Security

Do not open a public issue for a vulnerability. `.github/SECURITY.md` explains how to report one
privately, and lists the design decisions that are deliberate rather than defects.
