# Development

How to build, test, version and release this server. Who contributes is in
[`CONTRIBUTING.md`](../CONTRIBUTING.md), which GitHub links to from its issue and pull-request pages.

## Getting it running

The solution is `net8.0`, pinned by `global.json` to the 8.0.4xx SDK band. Nothing else is needed to
build; ffmpeg is optional and only affects video and audio metadata.

```powershell
dotnet build DlnaServer.sln          # must stay at 0 warnings
dotnet test  DlnaServer.sln
dotnet run --project src\DlnaServer.Host    # needs Library.SourceFolders set in config.json
```

`Docker.usage.md` covers the container path and `NasBuild.usage.txt` the QNAP deployment.

Every push and pull request is built and tested on Linux and Windows, the container is built and smoke
tested, and the whole set runs again every night; what runs when is in
[.github/workflows/README.md](../.github/workflows/README.md), with the commands to lint a workflow locally.

## The two hard rules

- **The build stays at 0 warnings.** CI builds with `-warnaserror`, so a warning is a failed build.
- **No package carries a known advisory.** `NuGetAudit` runs in `all` mode, transitive packages
  included. If an advisory appears in a package nothing here references directly, pin it in
  `Directory.Packages.props` the way `System.Security.Cryptography.Xml` is pinned, and say why.

Every project commits a `packages.lock.json`, and CI, the release, `NasBuild.sh` and the `Dockerfile` all
restore with `--locked-mode`. After changing `Directory.Packages.props` or a package reference, run a plain
`dotnet restore DlnaServer.sln` and commit the lock files it rewrites, or the next locked restore fails
with NU1004. The same goes for a new NAS runtime: add it to `RuntimeIdentifiers` in `Directory.Build.props`
first, because the lock files only carry targets for the runtimes listed there.

## Conventions

### The abstraction threshold

This project is disciplined about structure, and the risk that comes with that is ceremony: a small
problem grows an interface, an implementation, a test fixture and an architecture rule. Do not introduce
a new abstraction merely because a type is growing. Introduce one when at least one of these is true:

- it represents a real architectural boundary
- it has more than one implementation
- it needs an independent lifetime or DI registration
- it is independently testable behaviour
- it prevents an actual dependency violation
- it represents a meaningful domain concept

This is a brake on adding rules, not a loosening of the ones already here - those stay, and the
architecture tests still enforce them.

### Documentation explains why, tests enforce what

Anything a test already guarantees - the dependency directions, the entity and DTO boundary, the pinned
wire names, the database schema - is described in `docs/` as a fact with its reasoning, never restated as
a rule. A rule written in two places is a rule that will eventually disagree with itself, and the copy in
prose is the one that goes stale silently.

`CLAUDE.md` is the working description of the architecture and the conventions the code follows -
block-scoped namespaces, one type per file, `[LoggerMessage]` logging, entities that never leave the
persistence assembly, reads projected straight into DTOs. Read it before the first change; the
architecture tests in `tests/DlnaServer.ArchitectureTests` fail the build if the layering is broken,
so several of those conventions are enforced rather than merely documented.

`docs/decisions.md` is the decision record. If a change comes out of a decision, or if it cost time because of
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

Every project carries its own `<Version>`, in the form `Major.Minor.MonthDate` - the test projects
included. Bump the ones your change actually touched, and leave the rest alone - that is the whole point
of the number. `DlnaServer.Host` is the product version: it is what the release tag names and what the
About page shows. No other project tracks it.

If an operator would notice the change, add an entry to `release-notes.md` in their terms - it is not
a build log.

**Two documents state the current version and must move with it**: the version line near the top of
`README.md`, and the example release tag in this file (`docs/development.md`). `.github/SECURITY.md` carries a `Major.Minor`
support table that moves on a Minor bump only. Everything else that names a version - `docs/history.md`'s batch
headings, its trap entries, `release-notes.md`'s section headings - is a **historical reference** and is
correct as written. Do not sweep those; rewriting history to match the present is how a decision record
stops being one.

A release is a `v`-prefixed tag on the host's version (`v1.1.0928`); pushing it builds, tests and
attaches the linux-x64 archive, and publishes the container image to `ghcr.io/kopcot/dlnaserver` under that tag and `latest`, signed with cosign.

Every product version gets its tag, and it is set **when the version moves on, not while it is current**: before
the first change that bumps the host past `X`, the last commit that carried `X` is tagged `vX`. Several commits under
one version share that one tag, on the newest of them. Since 2026-09-30 `tag-release.yml` sets it on the push that
carries the bump and releases it, so **do not tag locally** - a local tag that differs from the one the workflow made
is refused by the next fetch. Pushing a `v*` tag by hand still releases it. The whole flow is in
[.github/workflows/README.md](../.github/workflows/README.md).

## Pull requests

Say what changed, why, and how you verified it - the template asks for exactly that. Keep the diff to
the change: unrelated reformatting and drive-by refactors make the real edit hard to find.

## Security

Do not open a public issue for a vulnerability. `.github/SECURITY.md` explains how to report one
privately, and lists the design decisions that are deliberate rather than defects.
