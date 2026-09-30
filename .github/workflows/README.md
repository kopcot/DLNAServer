# Workflows

What runs on GitHub Actions, when, and why - the one place CI and releases are documented; everything else
links here. Every workflow starts with `contents: read` and elevates only the job that needs more; every
checkout sets `persist-credentials: false`; every action is pinned to a commit with its version in a
comment, and Dependabot keeps those pins current. The traps that shaped them are in
[docs/decisions.md](../../docs/decisions.md) section 7, "CI and release traps".

## At a glance

| Workflow | Runs on | What it does | Needs more than read? |
| --- | --- | --- | --- |
| [`dotnet.yml`](dotnet.yml) | push and PR to `main`; nightly via `full-suite.yml` | Formatting check, Release build at 0 warnings, all tests on Linux **and** Windows, coverage summary on the run page | no |
| [`docker.yml`](docker.yml) | push and PR to `main` when anything the image reads changed; manual; nightly | Builds the image, starts it with host networking, smoke tests it, scans it with Trivy | no |
| [`codeql.yml`](codeql.yml) | push and PR to `main`; weekly | CodeQL security analysis of the C# (buildless, `security-extended`) | `security-events: write` |
| [`dependency-review.yml`](dependency-review.yml) | PR to `main` | Lists packages and actions a PR adds or removes | no |
| [`vulnerability-audit.yml`](vulnerability-audit.yml) | manual; nightly | Vulnerable NuGet packages, direct and transitive | no |
| [`workflow-lint.yml`](workflow-lint.yml) | push and PR that touch `.github/workflows/` | actionlint and zizmor over these files | no |
| [`scorecard.yml`](scorecard.yml) | push to `main`; weekly; branch-protection change | OpenSSF Scorecard of the repository's supply-chain posture | `security-events`, `id-token` |
| [`full-suite.yml`](full-suite.yml) | nightly 03:00 UTC; manual on any branch | Calls `dotnet.yml`, `docker.yml` and `vulnerability-audit.yml` | no |
| [`tag-release.yml`](tag-release.yml) | push to `main` that changes the host `<Version>` | Tags the version left behind, then calls the two below | `contents`, `packages`, `id-token` |
| [`release.yml`](release.yml) | a `v*` tag pushed by hand; called by `tag-release.yml` | linux-x64 archive, GitHub release with `release-notes.md`, smoke test of the downloaded archive | `contents: write` |
| [`docker-publish.yml`](docker-publish.yml) | a `v*` tag pushed by hand; called by `tag-release.yml` | Pushes `ghcr.io/kopcot/dlnaserver`, signs it with cosign, smoke tests it by digest | `packages`, `id-token` |

## On every change

`dotnet.yml` is the build gate. It enforces the two hard rules from [docs/development.md](../../docs/development.md):
the build stays at 0 warnings (`-warnaserror`), and no package carries a known advisory - `NuGetAudit` fails
the restore. It runs on Windows as well as Linux because the development box is Windows and path handling
is where the two differ. Formatting and the coverage summary run on Linux only; the sources and the tests
are the same on both. Coverage appears as the run's **job summary** - GitHub's own reporting, so there is
no Codecov account or token to look after - and the raw results are kept as an artifact for 14 days.

`docker.yml` runs only when something the image build reads changed, so a documentation-only change skips
it. The build uses a Buildx layer cache, so the restore layer is only redone when a project file changes.

`codeql.yml` and `dependency-review.yml` are the security checks on a pull request. **Dependency review is
blind to NuGet versions** - GitHub's dependency graph does not read `Directory.Packages.props` - so it only
lists packages and actions entering or leaving; `NuGetAudit` in `dotnet.yml` is what fails a vulnerable one.

## Nightly

`full-suite.yml` calls the build-and-test, container and audit workflows against `main` every night. The
base images, Debian's ffmpeg and the advisory database all change without a commit, and the per-change
workflows would only notice on the next change. Start it by hand from the Actions tab to run the whole set
against any branch - before cutting a release, for instance. It **calls** those workflows rather than
copying them, so the nightly run and the per-change run cannot drift apart.

## Releasing

A version is tagged when it is **left behind**: before the first change that bumps the host past `X`, the
last commit that carried `X` becomes `vX`. `tag-release.yml` does this for you:

```text
push to main that changes <Version> in src/DlnaServer.Host/DlnaServer.Host.csproj
  └─ tag-release.yml
       ├─ walks main's first-parent history of the push
       ├─ tags the commit before each version change as v<old version>
       │    (a tag that already exists is left alone)
       ├─ release.yml         build + test at the tag, publish linux-x64, GitHub release,
       │                      download it back and smoke test it
       └─ docker-publish.yml  build at the tag, push :v<version> and :latest to GHCR,
                              sign with cosign, pull by digest and smoke test it
```

**Do not create release tags locally any more** - push the version bump and the workflow tags the right
commit. A tag created locally with a different object from the one the workflow made is refused by the
next `git fetch` ("would clobber existing tag").

Pushing a `v*` tag by hand still works and runs `release.yml` and `docker-publish.yml` directly, which is
the way to release a version that is *not* being left behind, or to redo a failed one.

**Why the workflow calls the other two instead of letting the tag trigger them.** A tag pushed with the
workflow's own token starts no other workflow - GitHub's guard against loops. Calling them directly with the
tag as an input avoids needing a personal access token as a secret.

Image tags use the git tag as-is rather than a semver pattern: `1.1.0928` has a leading zero, which is not
valid semver, and a semver pattern would silently produce no tag.

## The smoke test

[`../scripts/smoke-test.sh`](../scripts/smoke-test.sh) is shared by `docker.yml`, `docker-publish.yml` and
`release.yml`. Each starts the server its own way - the image it just built, the image it just pushed, or
the archive downloaded from the release - with no media configured, so the server falls back to serving
its own folder. The script then checks:

- `/health/ready` on the media port - the database migrated and the configuration validated;
- `/` on the media port returns `description.xml` advertising a `MediaServer`, the first thing a television fetches;
- `/admin` renders on the admin port;
- `/admin` on the **media** port is a 404 - the two-port split is what keeps the admin UI away from renderers on the LAN.

**SSDP discovery is not tested.** It is UDP multicast, and a runner has no television on its network to
answer; the HTTP half is what a runner can honestly check.

Run it locally against a container:

```powershell
docker build -t dlna-server:ci .
docker run --detach --name smoke -p 26852:26852 -p 26853:26853 dlna-server:ci
bash .github/scripts/smoke-test.sh
docker rm -f smoke
```

## Linting these files

`workflow-lint.yml` runs both tools on every change to this folder; run them locally before pushing:

```powershell
uvx --from actionlint-py actionlint
uvx zizmor .github/workflows
```

`full-suite.yml` and `tag-release.yml` suppress zizmor's `self-repository` finding on their `uses: ./...`
lines. zizmor prefers the newer `$/...` form because a `./` **action** can be loaded from files an earlier
step wrote to disk. These are reusable **workflow** calls in jobs with no steps, which GitHub resolves from
the pushed commit, and actionlint 1.7.12 still rejects `$/` as an invalid reference - switch once it accepts it.

## Where the results appear

| Result | Where |
| --- | --- |
| Build, test, smoke test, Trivy, audit, lint | the run's log in the Actions tab; a failure fails the check on the pull request |
| Coverage summary | the **job summary** of `dotnet.yml`'s Linux run |
| Test results (`.trx`) and raw coverage | artifact `test-results-<os>`, kept 14 days |
| CodeQL and Scorecard findings | **Security → Code scanning** |
| Scorecard score | public, at `securityscorecards.dev` - the action requires `publish_results` for its signed upload |
| Releases | **Releases** - `DlnaServer-v<version>-linux-x64.zip`, notes from `release-notes.md` |
| Images | **Packages** - `ghcr.io/kopcot/dlnaserver:v<version>` and `:latest` |

A published image is signed by digest, keyless, with the identity of `docker-publish.yml`;
[`../SECURITY.md`](../SECURITY.md) has the `cosign verify` command.

## When a run fails

| Failed step | Usual cause | What to do |
| --- | --- | --- |
| `Build` | a compiler or analyzer warning - the build is `-warnaserror` | build locally with `-warnaserror` and fix it; do not suppress it |
| `Build`, with an `NU1901`-`NU1904` code | `NuGetAudit` found an advisory - restore only warns, the build replays it as an error | bump or pin the package in `Directory.Packages.props`, as `docs/development.md` describes |
| `Verify formatting` | the `.editorconfig` formatter disagrees | `dotnet format DlnaServer.sln` and commit the result |
| `Smoke test` | the server did not start, or a check failed | read the `Container log` / `Server log` step that follows - it runs even on failure |
| `Scan the image` | a CRITICAL or HIGH vulnerability **with a fix** in the image | rebuild on a newer base image - the Dockerfile tags float, so a rerun often clears it once Debian ships the fix |
| `List vulnerable packages` | a new advisory against a package already in use | as for the `NU190x` build failure |
| `Tag each version the push moved past` created nothing | the tag already existed, or the push did not change the host `<Version>` | the step's log names each tag it skipped |
| A release or image job after the tag failed | anything in the release or image build | if the cause was transient, **Re-run failed jobs** on that `Tag and release` run - pushing the tag again starts nothing, because GitHub already has it. If the tagged commit itself does not build, that version is not released: the fix is a later commit, so it ships as the next version |

## Adding or changing a workflow

- Start from `permissions: contents: read` and grant more only on the job that needs it, with a comment
  saying why.
- `persist-credentials: false` on every checkout; nothing here pushes with git.
- Pin every action to a full commit, with the version in a trailing comment, as the existing ones are -
  Dependabot updates them weekly.
- Pass a `${{ }}` value into a `run:` block through `env:`, never inline, so it cannot be parsed as shell.
- A workflow `full-suite.yml` or `tag-release.yml` calls needs `workflow_call`, and a literal in its
  concurrency group - `docs/decisions.md` section 7, "CI and release traps", says why.
- Run actionlint and zizmor locally, then update the table at the top of this file.

## Once, by hand

- **Make the GHCR package public** after the first image is published - a new package is private, and
  pulling it then needs a login.
- **Branch protection**, if you require checks: the build check is now `Build and test (ubuntu-latest)` and
  `Build and test (windows-latest)`, not `Build and test`.
- **Dependency graph** must be on for `dependency-review.yml`.
- **Trivy's database** comes from `ghcr.io/aquasecurity/trivy-db` because the default `mirror.gcr.io` copy
  answered 404 on 2026-09-30 and Trivy failed without falling back.
