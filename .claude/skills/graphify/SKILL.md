---
name: graphify
description: Build, refresh and query the graphify knowledge graph over the DLNAServer source. `/graphify update` re-extracts changed files (SHA256-cached, AST-only, no LLM cost); `/graphify query|explain|path|affected|god-nodes` pass through to the graphify CLI. Use when asked to "update graphify", "rebuild the graph", "refresh the knowledge graph", "where is X used", "what calls X", or `/graphify`.
argument-hint: "update|query|explain|path|affected|god-nodes [args]"
allowed-tools: Bash, Read
---

# Graphify (knowledge graph for this repository)

One graph, covering the nine projects - `src/` and `tests/` both. Builds are
**AST-only**, deterministic, SHA256-cached, and cost no tokens. There is no git repository here, so
nothing is driven off a diff - `update` re-extracts whatever changed on disc.

| Path | What it is |
|------|------------|
| `graphify-out/graph.json` | the graph every query reads |
| `graphify-out/GRAPH_REPORT.md` | hubs, communities, surprising connections |
| `graphify-out/graph.html` | interactive view, open it in a browser |
| `.graphifyignore` | the scope - edit it and re-run `update` to change what is graphed |

**The graph is built from the repository root.** Every default in the tool - the CLI's `--graph`,
the PreToolUse hook-guard - resolves `graphify-out/` against the working directory, and the working
directory is the repository root. Building it anywhere else makes both of those look in the wrong
place: the hook goes silently inert and every query needs an explicit `--graph`. The scope is
enforced by `.graphifyignore`, not by where the output lands.

## Routing - pick the mode from the first word of `$ARGUMENTS`

| First word | Mode |
|------------|------|
| `update` | [Update](#update) |
| `query` / `explain` / `path` / `affected` / `god-nodes` | [CLI passthrough](#cli-passthrough) |
| *(empty)* | `graphify-out/graph.json` exists → `update`, otherwise → [Install](#install-first-time-only) then `update` |

## Install (first time only)

The CLI is the PyPI package **`graphifyy`** (two y's), installed with a PATH-safe tool. Plain `pip`
leaves the executable off PATH on this box.

```bash
command -v graphify || uv tool install graphifyy
```

`uv tool upgrade graphifyy` updates it later.

## Update

Run it from the repository root, with `.` as the target:

```bash
graphify update .
```

A full build takes roughly four minutes; an incremental one is seconds, because only files whose
SHA256 changed are re-extracted. Deleted files are pruned automatically.

Read the output like this:

- `AST extraction: N/M uncached files` - progress, do not echo every line.
- `Rebuilt: N nodes, E edges, C communities` - **success**, report these numbers.
- `No files changed since last run. Nothing to update.` - already current.
- `N file(s) not classified` - files with no tree-sitter grammar (`.props`, `.xml`, `.editorconfig`).
  Expected, not a failure.

Add `--no-cluster` for a faster refresh when only queries matter - clustering feeds the report's
hub/community view, not query traversal. Re-cluster later with `graphify cluster-only`.

## CLI passthrough

From the repository root no `--graph` is needed; every verb finds `graphify-out/graph.json` itself.

```bash
graphify query     "how does a Browse request reach the repository"
graphify explain   "LibraryScanner"
graphify path      "ContentDirectoryService" "MediaFileRepository"
graphify affected  "IMediaFileRepository"
graphify god-nodes --top 15
```

Run the verb the user actually asked for - do not collapse everything into `query`. Forward their
`--dfs` / `--budget N` verbatim; supply `--budget 1500` only when they gave none, or `--budget 500`
for a narrow question.

Answers cite `source_file` and `source_location` and tag each edge `EXTRACTED` / `INFERRED` /
`AMBIGUOUS`. Treat EXTRACTED as fact, INFERRED as a lead, AMBIGUOUS as "verify first". If the graph
does not hold the answer, say so rather than inventing an edge.

## Traps

- **A non-zero exit code means nothing on its own.** Every graphify invocation returns 255 here,
  `--help` included. Judge success by the output - the `Rebuilt:` line, or the answer text.
- **`.razor` files are in the graph**, so the Blazor admin pages are queryable by name
  (`Maintenance.razor`, `Preview.razor`).
- **`docs/decisions.md` is in the graph too**, and its headings appear as nodes joined by `INFERRED`
  `references` edges. Useful for "which milestone covers X", misleading if read as a code edge.
- **Doc, image and paper changes are not picked up** by the AST-only build - that needs the full
  semantic pipeline, which costs tokens and is not configured here.
- **The graph is derived data.** Deleting `graphify-out/` costs one rebuild, nothing else.

## The hook-guard

`.claude/settings.local.json` carries a `PreToolUse` hook matching `Read|Glob` and running
`graphify hook-guard read`. It nudges toward the graph before a raw file read, injecting roughly
75-100 tokens into each matched call, and stays silent when no graph exists.

```jsonc
{ "hooks": { "PreToolUse": [
  { "matcher": "Read|Glob", "hooks": [{ "type": "command", "command": "graphify hook-guard read" }] }
] } }
```

Three things about it are deliberate:

- **`settings.local.json`, not `settings.json`** - it is a personal preference, and the git-ignored
  file is where a personal preference belongs.
- **`Read|Glob`, never `Bash`** - a `Bash` matcher taxes every build, test run and shell command for
  almost no navigation value.
- **The bare `graphify` command, not an absolute path** - bash mangles the backslashes in a Windows
  path into `command not found`.

Claude Code snapshots hooks at session start, so a change to that file takes effect on the **next**
session, not this one.
