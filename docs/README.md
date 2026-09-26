# Documentation

`README.md` says what this is. These say how it works. `decisions.md` says why it is built this way and what
is still owed, `history.md` records what changed and when, and `release-notes.md` says what changed for
whoever runs the server.

| Document | What it answers |
| --- | --- |
| [Architecture](architecture.md) | How the solution is laid out, what may depend on what, and what the naming means |
| [DLNA compatibility](dlna-compatibility.md) | What goes on the wire, and every place it differs from the reference |
| [Configuration](configuration.md) | Every setting, where it is read from, and which need a restart |
| [Operations](operations.md) | Running it, feeding it, and finding out what it is doing |
| [Performance](performance.md) | The decisions taken for a NAS with mechanical drives and limited memory |
| [Troubleshooting](troubleshooting.md) | It is not working - start here |
| [Decisions, conventions and traps](decisions.md) | Why it is built this way, and the rules for changing it |
| [Status and history](history.md) | What is deployed, and the batch-by-batch record |
| [Development](development.md) | Building, testing, versioning and releasing |
| [Docker](../Docker.usage.md) | Running it in a container, and why it needs host networking |
| [NAS build](../NasBuild.usage.txt) | Publishing for the QNAP NAS: arguments, ports, configuration |

## What belongs where

The split that keeps these from turning back into one long file:

**Documentation explains *why*. Tests enforce *what*.** Anything already guaranteed by
`tests/DlnaServer.ArchitectureTests`, a golden wire test or an EF migration is named here as a fact and
its rationale given - never restated as a rule, because a rule written twice is a rule that will disagree
with itself. The dependency directions, the entity and DTO boundary, the pinned wire names and the
database schema are all in that category.

**`decisions.md` and `history.md` hold the internal record.** A document here says what the server does
today; `decisions.md` says why, and carries the conventions, the traps and the open work in section 7b;
`history.md` says how it came to be that way, batch by batch. Both were split out of `PLAN.md` on
2026-09-23.

**`release-notes.md` is for the operator**, one entry per `Major.Minor`, in their terms. A change they
would not notice adds nothing to it.
