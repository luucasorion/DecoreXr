# ADR 0015 — Git / branching workflow

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion

## Context
The project needs a predictable, reviewable change process with issues, PRs, milestones, and a
protected integration branch.

## Decision
Use a **`dev`-based workflow**: `main` is the stable line, `dev` is the integration branch.
Every change branches **off `dev`**, opens a **PR into `dev`** whose body contains `Closes #n`,
and is **merged by the maintainer** (lucas.orion), not by Claude. **One PR in flight at a time**
— finish it, wait for merge, then branch the next off `dev`. Do not stack PRs. Each PR's
milestone + project are set to match its issue. Issues are closed manually (a `dev` merge won't
auto-close).

## Rationale
Keeps `main` releasable, serializes review to one change at a time, and preserves human control
over merges and issue closure.

## Consequences
- Positive: reviewable, serial, auditable; clear traceability from issue → PR → merge.
- Negative / risks: serial PRs are slower than parallel work; acceptable for a solo project with
  a single reviewer.

## Alternatives Considered
- **Commit straight to `main`** — rejected: no review gate, `main` not protected.
- **Multiple stacked PRs** — rejected: hard to review, merge-order fragile.

## Notes
On Windows/PowerShell, write `gh`/`git` multi-line bodies to a file and use `-F`/`--body-file`.
The `/issue` skill and `adr-guardian` agent operate against this workflow.
