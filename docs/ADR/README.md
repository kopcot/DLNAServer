# Architecture decision records

Every standing decision about this server lives here, one file per decision. This folder is the project's decision record, and the only one: the standing decisions were moved here from `docs/decisions.md` section 7b on 2026-10-26, and that section now points here instead of carrying a second copy. `decisions.md` keeps the conventions, the traps and the open work; `history.md` keeps the batch-by-batch record of how each decision came about.

**Reporting a standing decision as a defect is a known false positive.** Each of these was decided by the maintainer with its reasons, many of them after a review had raised the opposite. A review finding that contradicts one is answered by the ADR, not re-investigated - unless the finding brings evidence the ADR did not have, such as a measurement that contradicts it or a change in the threat model.

## Format

- **File name:** `NNNN-kebab-case-title.md`, four digits, numbered in the order the decisions were taken.
- **Numbers 0001 to 0033 are the old standing-decision numbers**, so a reference to "standing decision 20" in a source comment, a test or `history.md` means [0020](0020-avi-maps-to-video-x-msvideo.md). Each of those files says so in its header.
- **Sections:** Context (why the question came up), Decision (what was decided, in the words it was recorded in), Consequences (what follows from it, and what not to report).
- **Status values:** `Proposed` (written down, not yet agreed), `Accepted` (in force), `Superseded by NNNN` (replaced; the file stays so old references still resolve), `Deprecated` (no longer applies, nothing replaces it).
- **A new decision takes the next free number.** A decision that changes an old one is a new ADR that supersedes it; the old file is not rewritten, only its status line changes.

## Index

| ADR | Title | Status | Date |
| --- | --- | --- | --- |
| [0001](0001-no-authentication.md) | No authentication anywhere | Accepted | before 2026-09-08 |
| [0002](0002-allowed-hosts-wildcard-is-a-signal.md) | `AllowedHosts: *` is a signal, not a wildcard | Accepted | before 2026-09-08 |
| [0003](0003-media-byte-cache-machinery-is-inert.md) | The media byte-cache machinery is inert, not dead | Accepted | before 2026-09-08 |
| [0004](0004-central-thumbnail-cache-kept-unreachable.md) | `Thumbnails.CacheDirectory` and the central-cache branch are kept | Accepted | before 2026-09-08 |
| [0005](0005-not-equal-null-in-ef-projections.md) | `!= null` inside an EF projection is correct | Accepted | before 2026-09-08 |
| [0006](0006-block-scoped-namespaces-and-protocol-xml-docs.md) | Block-scoped namespaces; protocol contracts carry XML docs | Accepted | before 2026-09-08 |
| [0007](0007-first-fill-dating-not-gated-on-file-creation-setting.md) | First-fill index dating is not gated on `UseFileCreationDateTime` | Accepted | before 2026-09-08 |
| [0008](0008-no-max-pool-size.md) | `Max Pool Size` was tried and reverted | Accepted | before 2026-09-08 |
| [0009](0009-issourceroot-index-kept.md) | `IsSourceRoot`'s index is kept | Accepted | before 2026-09-08 |
| [0010](0010-admin-listings-page-the-data.md) | The admin listings page the data | Accepted | before 2026-09-08 |
| [0011](0011-nas-byte-cache-limits-by-choice.md) | The NAS byte cache stays at 5120 / 512, by choice | Accepted | before 2026-09-08 |
| [0012](0012-whole-file-buffering-survives.md) | Whole-file buffering in the byte cache survives | Accepted | 2026-09-03 |
| [0013](0013-code-defaults-match-shipped-config.md) | The code defaults match the shipped `config.json` | Accepted | 2026-09-03 |
| [0014](0014-nas-database-memory-settings-by-choice.md) | The NAS database memory settings are the operator's choice | Accepted | 2026-09-08 |
| [0015](0015-transfer-mode-follows-the-flags.md) | `transferMode.dlna.org` follows the flags | Accepted | 2026-09-08 |
| [0016](0016-mime-inferred-from-the-built-in-catalog.md) | Scanning infers a MIME from the built-in catalog | Accepted | 2026-09-08 |
| [0017](0017-source-folders-compared-ordinally.md) | `IsUnderFirstFillRoot` compares ordinally | Accepted | 2026-09-08 |
| [0018](0018-addrangeasync-kept-for-tests.md) | `IMediaFileRepository.AddRangeAsync` is kept | Accepted | 2026-09-08 |
| [0019](0019-sql-exclusion-predicate-stays.md) | The SQL exclusion predicate stays as it is | Accepted | 2026-09-08 |
| [0020](0020-avi-maps-to-video-x-msvideo.md) | `.avi` maps to `video/x-msvideo` | Accepted | 2026-09-08 |
| [0021](0021-ovecka-shaun-3-is-corrupt-on-disc.md) | `ovecka.shaun.3.avi` is corrupt on disc | Accepted | 2026-09-08 |
| [0022](0022-manage-on-the-media-port.md) | `/manage` lives on the media port | Accepted | 2026-09-08 |
| [0023](0023-no-bind-address.md) | `ServerOptions` has no bind address | Accepted | 2026-09-08 |
| [0024](0024-required-cancellation-token-on-private-helpers.md) | Private and internal `async` helpers take a required `CancellationToken` | Accepted | 2026-09-08 |
| [0025](0025-dlna-org-op-bit-naming-is-correct.md) | The `DLNA.ORG_OP` flag-bit naming is correct | Accepted | 2026-09-08 |
| [0026](0026-file-moved-into-excluded-folder-loses-its-row.md) | A file moved into an excluded folder has its row removed | Accepted | 2026-09-08 |
| [0027](0027-generated-thumbnail-content-stays-byte-array.md) | `GeneratedThumbnail.Content` stays a `byte[]` | Accepted | 2026-09-08 |
| [0028](0028-check-then-open-gap-accepted.md) | Accepted limit - delivery checks the path, then opens it again | Accepted | 2026-09-26 |
| [0029](0029-admin-port-answers-local-network-only.md) | The admin port answers the local network only | Accepted | 2026-09-26 |
| [0030](0030-kestrel-header-limits-at-defaults.md) | Kestrel's request-header limits stay at their defaults | Accepted | 2026-09-26 |
| [0031](0031-stop-refused-while-reset-pending.md) | Stop is refused while a database reset is pending | Accepted | 2026-09-26 |
| [0032](0032-release-tags-created-by-tag-release.md) | Release tags are created by `tag-release.yml` | Accepted | 2026-09-30, revised 2026-10-01 |
| [0033](0033-smoke-test-skips-ssdp-discovery.md) | The smoke test does not exercise SSDP discovery | Accepted | 2026-09-30 |
| [0034](0034-compose-image-name-on-docker-hub.md) | Compose files name the image `kopcot/dlnaserver:latest` | Accepted | 2026-10-26 |
| [0035](0035-ci-cd-failures-and-their-fixes.md) | CI/CD failures and their fixes | Accepted | 2026-10-26 |

0035 is a catalogue rather than a single decision: every CI/CD failure the repository has had, with the run ids, the error, the fix commit and how to keep it from coming back. The decisions it led to are recorded in it and, where they have their own ADR, linked.
