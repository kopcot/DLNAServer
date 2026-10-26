# 0035. CI/CD failures and their fixes

- **Status:** Accepted
- **Date:** 2026-10-26

## Context

Between the first CI run on 2026-09-21 and 2026-10-26 the repository's GitHub Actions had 57 failed runs. They come from 20 root causes. Most were fixed in a commit whose message explains it, and some taught a trap that is already in [decisions.md](../decisions.md) section 7, *CI and release traps*, but nothing tied a failed run to its cause and its fix. The next time one of these errors appears, the question is "have we seen this before", and this file answers it.

The catalogue was collected on 2026-10-26 from every failed run in `gh run list --status failure` (each read with `--log-failed`), `git log`, the pull requests, [.github/workflows/README.md](../../.github/workflows/README.md), [decisions.md](../decisions.md) section 7 and [history.md](../history.md) 6p, 6v, 6x and 6y. A run that failed only because of an earlier entry's cause - a Dependabot pull request opened while `main` was red, for instance - is listed under that entry. Cancelled runs are not failures and are not listed.

## Decision

Every CI/CD failure gets an entry here: the workflow and job, the run ids, the error as the log printed it, the root cause, the fix commit and what stops it coming back. A new failure with a new cause takes the next entry number; a recurrence adds its run id to the existing entry.

The durable rules these failures produced, each recorded where it lives:

| Rule | From entry | Where it is kept |
| --- | --- | --- |
| A framework move is a deliberate change, never a Dependabot bump - major versions of the .NET base images are ignored | 3 | `.github/dependabot.yml` |
| Every change is built and tested on Linux **and** Windows | 4, 6 | `dotnet.yml`, [workflows README](../../.github/workflows/README.md) |
| `SkiaSharp` and its native Linux package arrive in one Dependabot group | 6 | `.github/dependabot.yml` |
| A one-off red concurrency test is a defect report, not noise | 7, 8 | this file |
| Lint workflows with `actionlint -shellcheck` and `zizmor .` from the repository root, with a token | 9, 10 | [workflows README](../../.github/workflows/README.md) |
| The runtime image runs `apt-get upgrade`, and that layer is never cached | 12, 14 | `Dockerfile`, `docker.yml` |
| Release tags are made by `tag-release.yml`, on the pushed head | 13, 19 | [0032](0032-release-tags-created-by-tag-release.md) |
| After any package change, `dotnet restore DlnaServer.sln --force-evaluate` and commit every lock file it rewrites | 15 | [development.md](../development.md) |
| A transitive advisory is fixed by pinning the package by hand, never by merging a Dependabot pin across a major version | 16, 17 | [development.md](../development.md), "The two hard rules" |
| Every SHA-pinned action carries its exact `# vX.Y.Z` comment | 18 | the workflows themselves |

## Consequences

When a run goes red, look its error up here first. A known cause means a known fix; a recurrence of a "fixed" entry means the fix did not hold and the entry is reopened rather than duplicated.

## The catalogue

### 1. Source files never committed - `.gitignore`'s `thumbnails/` swallowed `Processing/Thumbnails/`

- **Workflow / job:** `.NET` (`dotnet.yml`) build; `Release` (`release.yml`) `Publish linux-x64`; the container build job
- **Runs:** first 2026-09-21, run 35640879449 - the first CI run ever. Also 35640963853, 35641472351, 35695565060, every Dependabot pull request opened 2026-09-22 06:36-06:47 (35695621090, 35695626637, 35695630645, 35695632419, 35695636137, 35695941219, 35696348317, 35696426764), and **Release 35695957898 on tag `v1.1.0922`**
- **Error:**
  `error CS0234: The type or namespace name 'Thumbnails' does not exist in the namespace 'DlnaServer.Media.Processing'`
  `error CS0246: The type or namespace name 'ImageThumbnailGenerator' could not be found`
- **Root cause:** `.gitignore` carried `thumbnails/`, meant for the runtime preview cache. It also matched `src/DlnaServer.Media/Processing/Thumbnails/`, so `ImageThumbnailGenerator*.cs` and `ThumbnailRequest.cs` were never committed; local builds passed because the files existed on disc. The match is most likely down to the case-insensitive Windows checkout - that part is inferred, not reproduced.
- **Fix:** `eb27a38` (2026-09-22) removed the `thumbnails/` line and added the three files. Run 35696677190 on the pushed commit got past compilation. `v1.1.0922` was never released.
- **Prevent / detect:** a clean-clone CI build is the detector. Keep `.gitignore` entries for runtime state anchored (`/thumbnails/`) or specific enough that they cannot match a source folder; `git check-ignore -v <path>` explains a missing file.

### 2. CodeQL default setup ran before code scanning was enabled

- **Workflow / job:** `CodeQL` (dynamic, default setup) `Analyze (csharp)`
- **Runs:** 2026-09-21, run 35641471842 (only occurrence)
- **Error:** `Please verify that the necessary features are enabled: Code scanning is not enabled for this repository.`, followed by `This job attempted to run with improved incremental analysis but it did not complete successfully`
- **Root cause:** the repository had just been published, and code scanning was not yet switched on in Settings.
- **Fix:** a repository setting, so there is no commit to point at; default setup has run since. Why there is no `codeql.yml` is in entry 11.
- **Prevent / detect:** enable the code security settings before the first push of a new repository.

### 3. Dependabot proposed the .NET 10 `sdk` / `aspnet` base images - `global.json` pins 8.0.414

- **Workflow / job:** `.NET` container build, later `Docker`
- **Runs:** first 2026-09-22, run 35695623139 (PR #2, `dotnet/sdk` 8.0 -> 10.0); again 35702146220, 35787215328, 35789559743. PR #5 (`dotnet/aspnet` -> 10.0, run 35695632419) failed on entry 1 first.
- **Error:** `process "/bin/sh -c dotnet restore DlnaServer.sln" did not complete successfully: exit code: 155`, with `Install the [8.0.414] .NET SDK or update [/src/global.json] to match an installed SDK.` / `10.0.401 [/usr/share/dotnet/sdk]`
- **Root cause:** the solution targets `net8.0` and `global.json` pins SDK 8.0.414 (`rollForward: latestPatch`); a 10.0 SDK image cannot satisfy it.
- **Fix:** `fa39804` (2026-09-22) - `.github/dependabot.yml` ignores `version-update:semver-major` for `mcr.microsoft.com/dotnet/aspnet` and `.../sdk`; PRs #2 and #5 closed. `e8a5b9d` later pinned both images by digest.
- **Prevent / detect:** the Dependabot ignore rule. A framework move is a deliberate change. The pin itself must stay: without it the development box picked SDK 10 and the build went from 0 to 10 warnings (CA1873) - see the `global.json` entry in [history.md](../history.md).

### 4. Linux-only test failures the first Linux CI exposed

- **Workflow / job:** `.NET` `Build and test`, Test step
- **Runs:** 2026-09-22, run 35696677190
- **Error:**
  `Failed TryResolve_WithAnAbsoluteSubFolder_IsRefused` - `Expected boolean to be False because Path.Combine DISCARDS the root when the second part is rooted ... but found True.`
  `Failed IndexAsync_WithFileCreationDatesAndNoBirthTime_FallsBackToTheWriteTime` - `... but <1970-01-01> was off by ...`
- **Root cause:** (a) `UploadDestination` ran `Trim('/')` before the rooted check, so `/tmp/x` became the relative `tmp/x` on Linux, where Windows caught it on the `:` - a real defect. (b) The test set the creation time after the write time; Linux has no API to set a birth time, so .NET sets the write time instead and destroyed the value under test.
- **Fix:** `fa39804` (2026-09-22) - the rooted check runs before trimming, and the test sets the creation time first.
- **Prevent / detect:** the CI matrix builds and tests on both `ubuntu-latest` and `windows-latest` (added 2026-09-30, [history.md](../history.md) 6x), because path handling is where the development box and the NAS differ.

### 5. The Dockerfile copied `config.json` from a path the build stage never wrote

- **Workflow / job:** `.NET` container build (buildx)
- **Runs:** 2026-09-22, run 35696677190 (the same run as entry 4)
- **Error:** `failed to calculate checksum of ref ...: "/app/config.json": not found` at `[runtime 5/7] COPY --from=build /app/config.json ./config.json`
- **Root cause:** the build stage's `WORKDIR /src` put the file at `/src/app/config.json`. Nothing had built the image since the path changed, so the mismatch sat unseen.
- **Fix:** `fa39804` - `COPY --from=build /src/app/config.json ./config.json`. `config.json` stopped shipping from the repository in `9ebf3d4` (2026-09-23).
- **Prevent / detect:** build the image in CI on every change the image reads (`docker.yml`'s path filter, `245ffba`) and smoke test it (`.github/scripts/smoke-test.sh`, `6f3e01e`).

### 6. The SkiaSharp managed package bumped without its native Linux package

- **Workflow / job:** `.NET` `Build and test`, Test step - the build itself is green
- **Runs:** first 2026-09-22, run 35703112478 (PR #11, SkiaSharp 3.119.1 -> 4.152.1); again 35787105760, 35789574067; recurred 2026-09-29 in runs 36605491824 (PR #16) and 36605524892 (PR #17, the native half alone)
- **Error:** `Failed Read_ForAJpeg_ReturnsWhatThePictureSaysAboutItself` - `The version of the native libSkiaSharp library (119.0) is incompatible with this version of SkiaSharp. Supported versions of the native libSkiaSharp library are in the range [152.0, 153.0).`
- **Root cause:** Dependabot opens one pull request per package. `SkiaSharp` and `SkiaSharp.NativeAssets.Linux.NoDependencies` must move together; the mismatch surfaces only at run time, and only on Linux - a Windows run never loads the Linux native package.
- **Fix:** `14a4e21` / `09cb9c1` (2026-09-23) moved both to 4.152.1 with a comment in `Directory.Packages.props` (PR #11 closed); `ec84477` (2026-09-29) bumped the native half by hand inside PR #16 (PR #17 closed); `cd5f3fe` (2026-10-17) added a `skiasharp` Dependabot group so both arrive in one pull request (PR #32, merged green).
- **Prevent / detect:** the Dependabot group. The Linux test leg is the only detector.

### 7. Flaky `LibraryIndexLockTest.RunAsync_WithASecondCallerWaiting_DoesNotOverlap`

- **Workflow / job:** `.NET` `Build and test` (Linux)
- **Runs:** first 2026-09-22, run 35787218688; also 35789471115 (push to `main`), 35789559743, 36254223133 (2026-09-26, push to `main`). **Four failures**, although [history.md](../history.md) 6v says it "failed once".
- **Error:** `Expected overlapped to be False because the first operation had to have finished before the second was let in, but found True.`
- **Root cause:** a test race, not a lock defect. The lock releases its permit a moment before `RunAsync`'s task completes, and the waiting caller resumes on the pool, so reading `first.IsCompleted` could catch that gap. It never reproduced locally in 5,000 repetitions.
- **Fix:** `7e78bd4` (2026-09-27) - the test flags the end of the first operation itself instead of reading the task's completion. No recurrence in any later run.
- **Prevent / detect:** in a concurrency test, assert on a signal the operation sets, never on `Task.IsCompleted` of the thing that releases the gate.

### 8. Flaky `ServedFileCacheTest.LoadAsync_WhenCalledConcurrentlyForOnePath_SharesOneReadAndOneBuffer`

- **Workflow / job:** `.NET` `Build and test (ubuntu-latest)`; the `Full test suite` nightly
- **Runs:** first **2026-09-22, run 35787209990** (PR #1, setup-dotnet 6 - shrugged off at the time); again 2026-10-25, run 40174890222 (the nightly on `6ca9e72`). The nightlies either side, 40075586012 and 40291763808, were green.
- **Error:** `Expected collection to contain 1 item(s) because all 32 callers must await the same read and receive a view over one buffer - a distinct array per caller would mean 32 disc reads and 32 allocations for one file, but found 2`
- **Root cause:** a real, benign defect in `ServedFileCache.LoadAsync` - a gap between `TryGet` and `GetOrAdd`. A read finishing and retiring its in-flight marker inside that gap made a second caller start a second disc read.
- **Fix:** `d464f49` (2026-10-26) - `ReadAndStoreAsync` re-checks `TryGet` first; Host 1.2.1026, Core 1.1.1026, released as `v1.2.1026`. Before the fix the race reproduced locally at iteration 217 of a 2,000-iteration loop; after it, four loops of 2,000 were green, and the push run 40361654805 on `d464f49` was green.
- **Prevent / detect:** the test already detects it; only the nightly reruns it often enough. Treat a one-off red concurrency test as a defect report, not noise - this one was first seen five weeks before it was fixed.

### 9. actionlint failed on the runner while reporting clean locally - shellcheck not installed

- **Workflow / job:** `Workflow lint` (`workflow-lint.yml`) `actionlint and zizmor`, actionlint step
- **Runs:** 2026-09-30, run 36674208135 (only occurrence)
- **Error:** `.github/workflows/docker-publish.yml:86:9: shellcheck reported issue in this script: SC2086:info:1:51: Double quote to prevent globbing and word splitting [shellcheck]`
- **Root cause:** an unquoted `${DIGEST}` in the cosign step. Locally actionlint had no shellcheck on the `PATH`, and without it actionlint **silently skips every `run:` script** and reports clean; the runner has shellcheck.
- **Fix:** `95a1d57` (2026-09-30) quoted `"{}@${DIGEST}"`, and the workflows README's local lint commands pass `-shellcheck="$sc"`, with shellcheck from `uvx --from shellcheck-py`.
- **Prevent / detect:** always lint with `uvx --from actionlint-py actionlint -shellcheck="$sc"`.

### 10. zizmor `dependabot-cooldown` - the local zizmor was scoped to the workflows folder

- **Workflow / job:** `Workflow lint`, zizmor step
- **Runs:** 2026-09-30, run 36675209783 (only occurrence)
- **Error:** `warning[dependabot-cooldown]: insufficient cooldown in Dependabot updates --> ./.github/dependabot.yml:6:5` - three findings, `Process completed with exit code 13`
- **Root cause:** the zizmor action audits the whole repository, `.github/dependabot.yml` included. The local run audited only `.github/workflows`, and without `GH_TOKEN` it skips the online audits.
- **Fix:** `c75aa1b` (2026-09-30) - `cooldown: default-days: 7` on the nuget, github-actions and docker ecosystems (security updates bypass it); `dependabot.yml` added to `workflow-lint.yml`'s path filter; the README runs `$env:GH_TOKEN = gh auth token; uvx zizmor .`.
- **Prevent / detect:** lint with `zizmor .` from the repository root, with a token.

### 11. Traps hit while building the workflows that never produced a failed run (2026-09-30)

Each is recorded in [decisions.md](../decisions.md) section 7, *CI and release traps*, or in the [workflows README](../../.github/workflows/README.md). None has a failed run in the history - the failure was caught before the push, or was a silent no-op - so the effect and the fix below come from those documents and the commit messages rather than from a run log.

| Trap | Effect | Fix and commit |
| --- | --- | --- |
| Trivy's default database mirror (`mirror.gcr.io`) answered 404 | Trivy fails without falling back - it reads as a failed scan and scanned nothing | `docker.yml` sets `TRIVY_DB_REPOSITORY` to `ghcr.io/aquasecurity/trivy-db` (`6f3e01e`) |
| A CodeQL workflow while CodeQL default setup is on | GitHub rejects the workflow's results | `cb29719` removed `codeql.yml`; default setup uses the *Extended* suite. Do not add a `codeql.yml` |
| Renaming a job renames its required check | branch protection waits for `Build and test` / `Container builds`, which nothing reports, and only an admin bypass merges | recorded in `cb29719`; the README's "Once, by hand" section has the `gh api` command. Still open per the README as read on 2026-09-30 |
| `1.1.0928` is not semver (leading zero) | `docker/metadata-action`'s `type=semver` produces **no tag**, silently | the image tag is `type=raw` from the git tag (`6f3e01e` / `52e1337`) |
| In a called workflow `github.workflow` is the caller's name | the three workflows `full-suite.yml` calls share one concurrency group and cancel one another | a literal in each called workflow's concurrency group (`52e1337`) |
| A tag pushed with `GITHUB_TOKEN` starts no other workflow | `release.yml` / `docker-publish.yml` never run | `tag-release.yml` calls them with a `tag` input (`52e1337`) |
| The Host project carried no `<Version>` before 2026-09-23 | a history walk would tag `v` | `tag-release.yml` skips it (`52e1337`; moot since `f41e348`) |
| actionlint 1.7.12 rejects `$/...`, zizmor asks for it (`self-repository`) | one linter fails whichever form is used | `./` kept, with an inline zizmor ignore on the five reusable-workflow calls (`52e1337`) - see [0032](0032-release-tags-created-by-tag-release.md) |
| Docker Desktop does not mount the mapped `T:` network drive | a linter container sees no files and reports clean | run the linters through `uvx`, not their images |
| `dotnet restore --runtime` replaces the `RuntimeIdentifiers` set | a locked restore refuses with NU1004 | `NasBuild.sh` passes `-p:RuntimeIdentifier` (`e8a5b9d`, 2026-10-01) |
| `packages.lock.json` included as a default item | Host and Admin publish two lock files to one path (NETSDK1152) | excluded from default items in `Directory.Build.props` (`e8a5b9d`) |

### 12. Trivy failed on `libexpat1` - the digest-pinned base image was never upgraded

- **Workflow / job:** `Docker` (`docker.yml`) `Container build`, `Scan the image` step
- **Runs:** 2026-10-01, run 36821227441 (the push of `b1f2a9b`)
- **Error:** `Total: 6 (HIGH: 6, CRITICAL: 0)` - `libexpat1 | CVE-2024-28757 | HIGH | fixed | 2.5.0-1+deb12u3 | 2.5.0-1+deb12u4`, plus CVE-2025-59375, CVE-2026-25210, -45186, -66046 and -93990
- **Root cause:** `e8a5b9d`, the same day, pinned `aspnet:8.0` by digest for Scorecard. A digest pin means `docker build --pull` no longer refreshes the base, so Debian's fixes wait for Microsoft's next rebuild.
- **Fix:** `f41e348` (2026-10-01) - `apt-get upgrade --yes` in the runtime stage before installing ffmpeg and curl, and the Dockerfile comment claiming `--pull` refreshed the base was corrected. The next push, run 36822574506, was green.
- **Prevent / detect:** Trivy in every image build; keep the `apt-get upgrade` line.

### 13. `tag-release.yml` 403 - the workflow token cannot tag an older commit once a workflow changed

- **Workflow / job:** `Tag and release` (`tag-release.yml`), `Tag the version left behind` / `Tag each version the push moved past`
- **Runs:** 2026-10-01, run 36821227763 (only occurrence)
- **Error:** `gh: Resource not accessible by integration (HTTP 403)`
- **Root cause:** the workflow tagged the version *left behind* - `7a8d007` as `v1.1.0928`. GitHub treats a new tag as creating every workflow file that differs from `main`'s tip, and `GITHUB_TOKEN` can never hold the `workflows` permission. `v1.1.0928` was never released.
- **Fix:** `f41e348` (2026-10-01) - tag the **pushed head** with its own version, unless that tag exists; the documents were updated with it. The same push tagged and released `v1.1.1001`, run 36822574993 green. The maintainer chose this over a PAT or an App token. Recorded as [0032](0032-release-tags-created-by-tag-release.md).
- **Prevent / detect:** a push carrying two version bumps releases only the newer one; to release another commit, push a `v*` tag by hand.

### 14. Trivy failed nightly on `libpcre2-8-0` - the Actions layer cache froze `apt-get upgrade`

- **Workflow / job:** `Full test suite` (`full-suite.yml`) -> `Container / Container build`, Scan step
- **Runs:** first 2026-10-04, run 37191430948; again 37293003657 (2026-10-05)
- **Error:** `Total: 1 (HIGH: 1, CRITICAL: 0)` - `libpcre2-8-0 | CVE-2026-103111 | HIGH | fixed | 10.42-1+deb12u1 | 10.42-1+deb12u2`
- **Root cause:** the `RUN apt-get update && apt-get upgrade` line and the pinned base digest never change, so `docker.yml`'s Buildx GHA cache reused the layer built on 2026-10-01 and Debian's later fixes never reached the CI image. `docker-publish.yml` uses no cache and was never affected.
- **Fix:** `b9dd637` (2026-10-06) - `no-cache-filters: runtime` on the `docker.yml` build; the SDK and restore stages stay cached. The next nightly, 37417623284, was green.
- **Prevent / detect:** any layer whose job is to pull fresh packages must be excluded from the cache.

### 15. NU1004 on Dependabot NuGet pull requests - only the directly-referencing project's lock file was regenerated

- **Workflow / job:** `.NET` `Build and test (ubuntu-latest / windows-latest)` restore; `Docker` `Container build` (`dotnet restore DlnaServer.sln --locked-mode`)
- **Runs:** first 2026-10-06, runs 37503785533 / 37503785632 (PR #21, Xabe.FFmpeg 6.1.1) and 37503806440 / 37503806520 (PR #22, Xabe.FFmpeg + Downloader); again 38614268647 / 38614268727 (PR #26, SkiaSharp 4.153.1), 38614487335 / 38614487398 (PR #27, the native package 4.153.1), and 39008905400 / 39008905453 (PR #32, the skiasharp group, first revision `9fba02e`)
- **Error:** `src/DlnaServer.Host/DlnaServer.Host.csproj : error NU1004: Mistmatch between the requestedVersion of a lock file dependency marked as CentralTransitive and the the version specified in the central package management file` and `error NU1004: The project references dlnaserver.media whose dependencies has changed.The packages lock file is inconsistent with the project dependencies so restore can't be r[un in locked mode]` (NuGet's own spelling)
- **Root cause:** every project commits `packages.lock.json` and CI restores `--locked-mode` (since `e8a5b9d`, 2026-10-01). Dependabot bumped `Directory.Packages.props` and refreshed only `DlnaServer.Media`'s lock file. Host and the three test projects reach the package transitively through Media and keep a `CentralTransitive` entry naming the old version. **A plain `dotnet restore` leaves those entries alone**, so it does not fix this.
- **Fix:** the lock files were regenerated with `dotnet restore DlnaServer.sln --force-evaluate`, committing the Host lock file and the three test-project lock files - `9caa39f`, `e8ed629` and `5049ddf` (2026-10-17), made on local `dependabot/...` branches that were never pushed, so those commits never ran in CI; PRs #21, #22, #26 and #27 were closed instead. `cd5f3fe` (2026-10-17) added the `skiasharp` group, and PR #32's next revision, `9363144` by Dependabot, touched the Host, Media and all three test lock files; its merge `6ca9e72` built green (run 39015118155). Why that revision covered all five lock files when `9fba02e` did not is not established.
- **Prevent / detect:** after any package change, run `dotnet restore DlnaServer.sln --force-evaluate` and commit every lock file it rewrites - [development.md](../development.md) says so.

### 16. NU1004 on the Dependabot security pull request - it pinned `System.Security.Cryptography.Xml` at 9.0.21

- **Workflow / job:** `.NET` restore; `Docker` `Container build`
- **Runs:** first 2026-10-13, runs 38463561268 / 38463561446 (PR #23, "Bump the nuget group with 1 update"); again 38464811699 / 38464811846 (PR #24) and 39008788116 / 39008788144 (PR #30, 2026-10-17)
- **Error:** `tests/DlnaServer.ArchitectureTests/DlnaServer.ArchitectureTests.csproj : error NU1004: Transitive dependency System.Security.Cryptography.Pkcs moved to be centraly managed invalidated the lock file.` - later revisions printed the `CentralTransitive` mismatch of entry 15
- **Root cause:** Dependabot's answer to the entry-17 advisories ("Pinned System.Security.Cryptography.Xml at 9.0.21", PR #30's body) - a 9.x pin on a `net8.0` solution, without regenerating the lock files it invalidated.
- **Fix:** PRs #23, #24 and #30 closed (#30 on 2026-10-17); superseded by `cd5f3fe`, which pins Xml 8.0.5 and regenerates the lock files itself (entry 17).
- **Prevent / detect:** do not merge a Dependabot security pin across a major version; fix the advisory by hand.

### 17. NU1902 / NU1903 advisories on `System.Security.Cryptography.Pkcs` 8.0.1 and `.Xml` 8.0.4, through SoapCore

- **Workflow / job:** `Full test suite` -> `Vulnerability audit / NuGet advisories` first, then every `.NET` `Build and test` and the container build
- **Runs:** first 2026-10-14, the nightly run 38534069416 - the daily audit saw it before any build did; then 38614114837 and 38614888627 (pull-request builds), 38614268727 / 38614487398 (as warnings beside entry 15), and the nightlies 38701898636 and 38869429641. No source change triggered it: a new advisory against an unchanged package.
- **Error:** `src/DlnaServer.Host/DlnaServer.Host.csproj : error NU1903: Package 'System.Security.Cryptography.Xml' 8.0.4 has a known high severity vulnerability, https://github.com/advisories/GHSA-7hqp-7c5m-4vq6` - also GHSA-c399-9853-6jmj, -r955-8q5j-3h83 and -vrv5-2jg9-4mmr; Pkcs 8.0.1 GHSA-846f-f764-76mx (high) and GHSA-2fmv-rv2c-59x7 (moderate)
- **Root cause:** NuGetAudit runs in `all` mode, transitive packages included, at `moderate`, and `-warnaserror` replays the restore warning as a build error. The packages arrive through SoapCore.
- **Fix:** `cd5f3fe` (2026-10-17) - pin `System.Security.Cryptography.Xml` 8.0.5 in `Directory.Packages.props`, which takes Pkcs to 8.0.2; regenerate the Host, Upnp and three test lock files; Host 1.2.1017 so `tag-release.yml` released the fixed image. The next nightly, 39013546685, was green, and `v1.2.1017` was released.
- **Prevent / detect:** `vulnerability-audit.yml` runs daily in the nightly precisely because NuGetAudit only runs on a push. Fix by pinning the transitive package, as "The two hard rules" in [development.md](../development.md) says.

### 18. zizmor `ref-version-mismatch` on a Dependabot action bump

- **Workflow / job:** `Workflow lint`, zizmor step
- **Runs:** 2026-10-14, run 38614114898 (PR #25, download-artifact 8.0.1 -> 8.0.2; only occurrence)
- **Error:** `warning[ref-version-mismatch]: action's hash pin has mismatched or missing version comment --> ./.github/workflows/dotnet.yml:91:82` (and `release.yml:76:82`; `tag points to commit cf430e030ddb`; exit code 13)
- **Root cause:** an action pinned by SHA whose trailing version comment did not name the version the SHA points to.
- **Fix:** `cd5f3fe` (2026-10-17) - upload-artifact pinned to v7.0.2, download-artifact to v8.0.2, and every pin comment names the exact version (checkout v7.0.1, setup-dotnet v6.0.0, build-push-action v7.4.0). PR #25 closed. Workflow lint was green on `cd5f3fe`, run 39008576967.
- **Prevent / detect:** keep `# vX.Y.Z` exact on every SHA pin; zizmor's auto-fix can write it.

### 19. A local tag clashes with the workflow's tag - "would clobber existing tag"

- **Workflow / job:** none - a local `git fetch`
- **Runs:** none; recorded 2026-10-01 and reproduced on 2026-10-26 with `git fetch --tags --dry-run origin`
- **Error:** `! [rejected] v1.1.0922  -> v1.1.0922  (would clobber existing tag)`
- **Root cause:** release tags used to be made by hand. The local `v1.1.0922` points at `e6e08ab`; the remote one at `8cb68f6`, a rewritten "Update sollution" commit on no branch. Since `f41e348` only `tag-release.yml` creates tags, so any local tag for the same version is a different object.
- **Fix:** a rule, not code - **never create release tags locally**; push the version bump and let `tag-release.yml` tag the head ([0032](0032-release-tags-created-by-tag-release.md)).
- **Prevent / detect:** `git fetch --tags --dry-run` shows any clash. Other tag drift seen on the way, not yet cleaned up: a local-only annotated `v1.1.0926` on `245ffba`, and on the remote a stray `1.1.0927` tag (no `v`) on `245ffba` carrying a GitHub release named `v1.1.0927`.

### 20. A local `dotnet format` reports `ENDOFLINE` on a Windows checkout

- **Workflow / job:** none in CI - `Verify formatting` runs on Linux only and has never failed; local only
- **Runs:** none; reproduced locally on 2026-10-26
- **Error:** `src\DlnaServer.Upnp\Didl\DidlCaptionInfo.cs(1,25): error ENDOFLINE: Fix end of line marker. Replace 2 characters with '\n'.`
- **Root cause:** `.editorconfig` says `end_of_line = lf` (CRLF there made `--verify-no-changes` fail on every file outside a Windows tree) and `.gitattributes` forces `eol=lf` for `*.cs`, `*.razor`, `*.json` and the rest - both added in `e6e08ab` (2026-09-22). Files checked out before that and never checked out again stay CRLF in the working tree while the index is LF: `git ls-files --eol` lists 20 files as `i/lf w/crlf`, among them `DidlCaptionInfo.cs`, four `.razor` pages, `admin.css`, the migrations and five `packages.lock.json`, on a machine with `core.autocrlf=true` in the global gitconfig. A file rewritten since then is LF and passes.
- **Fix:** none applied yet. Checking the affected files out again, so the working tree matches the attributes, should clear it - delete them and `git checkout -- <files>`; the index needs no change. That has not been run. CI is unaffected, because a Linux checkout is LF.
- **Prevent / detect:** `git ls-files --eol | grep w/crlf` lists the stragglers.

## Run index - 57 failed runs

| Entry | Runs |
| --- | --- |
| 1 | 35640879449, 35640963853, 35641472351, 35695565060, 35695621090, 35695626637, 35695630645, 35695632419, 35695636137, 35695941219, 35695957898, 35696348317, 35696426764 |
| 2 | 35641471842 |
| 3 | 35695623139, 35702146220, 35787215328, 35789559743 (container half) |
| 4, 5 | 35696677190 |
| 6 | 35703112478, 35787105760, 35789574067, 36605491824, 36605524892 |
| 7 | 35787218688, 35789471115, 35789559743 (test half), 36254223133 |
| 8 | 35787209990, 40174890222 |
| 9 | 36674208135 |
| 10 | 36675209783 |
| 12 | 36821227441 |
| 13 | 36821227763 |
| 14 | 37191430948, 37293003657 |
| 15 | 37503785533, 37503785632, 37503806440, 37503806520, 38614268647, 38614268727, 38614487335, 38614487398, 39008905400, 39008905453 |
| 16 | 38463561268, 38463561446, 38464811699, 38464811846, 39008788116, 39008788144 |
| 17 | 38534069416, 38614114837, 38614888627, 38701898636, 38869429641 |
| 18 | 38614114898 |

Entries 11, 19 and 20 have no failed run. Run 35789559743 failed on two causes and is counted under both 3 and 7.
