# Documentation

`README.md` says what this is. These say how it works. `PLAN.md` says what is being done next and why
each decision was taken, and `release-notes.md` says what changed for whoever runs the server.

| Document | What it answers |
| --- | --- |
| [Architecture](architecture.md) | How the solution is laid out, what may depend on what, and what the naming means |
| [DLNA compatibility](dlna-compatibility.md) | What goes on the wire, and every place it differs from the reference |
| [Configuration](configuration.md) | Every setting, where it is read from, and which need a restart |
| [Operations](operations.md) | Running it, feeding it, and finding out what it is doing |
| [Performance](performance.md) | The decisions taken for a NAS with mechanical drives and limited memory |
| [Troubleshooting](troubleshooting.md) | It is not working - start here |

## What belongs where

The split that keeps these from turning back into one long file:

**Documentation explains *why*. Tests enforce *what*.** Anything already guaranteed by
`tests/DlnaServer.ArchitectureTests`, a golden wire test or an EF migration is named here as a fact and
its rationale given - never restated as a rule, because a rule written twice is a rule that will disagree
with itself. The dependency directions, the entity and DTO boundary, the pinned wire names and the
database schema are all in that category.

**`PLAN.md` holds the internal record** - the reasoning behind a decision, the measurements, the traps
that cost time, and the open work. A document here says what the server does today; `PLAN.md` says how it
came to and what is still owed. Section 7b is the standing list.

**`release-notes.md` is for the operator**, one entry per `Major.Minor`, in their terms. A change they
would not notice adds nothing to it.
