## What this changes

<!-- One paragraph. What behaviour is different after this is merged. -->

## Why

<!-- The defect, the request, or the constraint that made it necessary. -->

## How it was verified

<!-- Tick what actually ran. A green build is not verification for config-shaped changes. -->

- [ ] `dotnet build DlnaServer.sln` - 0 warnings
- [ ] `dotnet test DlnaServer.sln` - all green
- [ ] Ran the server and exercised the change
- [ ] Confirmed on a real renderer (television / VLC) - required for anything that alters the wire output

## Housekeeping

- [ ] `<Version>` bumped in every project this changes (`DlnaServer.Host` is the product version)
- [ ] `release-notes.md` updated, if an operator would notice this
- [ ] an ADR added under `docs/ADR/` if a decision came out of it, `docs/decisions.md` if a trap did, and `docs/history.md` for the batch itself
