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

- [ ] `<Version>` in `Directory.Build.props` bumped
- [ ] `release-notes.md` updated, if an operator would notice this
- [ ] `docs/decisions.md` updated, if a decision or a trap came out of it, and `docs/history.md` for the batch itself
