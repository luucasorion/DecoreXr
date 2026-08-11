# ADR 0015 — Git / branching workflow

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion
- Updated: 2026-08-11 — added the milestone integration-branch rule (see Change log)

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

**Milestone runs use a per-milestone integration branch (rule).** A `/milestone m#` run does
**not** open a PR per task. Instead it creates one integration branch **`M#` off `dev`**, commits
each task to `M#` (every task still reviewed by `adr-guardian` before its commit, with the
`M#-T#` marker in the commit subject), and lands the whole milestone via **one maintainer-approved
PR `M# → dev`** at the end. "One PR in flight" and "maintainer merges, not Claude" still hold —
now at the milestone level. Single-task `/issue` runs keep the per-task-PR-into-`dev` flow above.
A task is "done" when its `M#-T#` marker is in the **`M#` branch** commit subjects; the milestone
is done when all its markers are on `M#` and the `M# → dev` PR is merged. Tasks needing on-device
verification (ADR 0011) may be deferred and the PR opened for the runnable subset.

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
The `/issue` skill and `adr-guardian` agent operate against this workflow; the `/milestone` skill
implements the milestone integration-branch rule above.

## Change log
- 2026-08-10 (initial): `dev`-based workflow, per-change PR into `dev`, one PR in flight,
  maintainer-merged.
- 2026-08-11 (update): milestone runs (`/milestone`) use a single `M#` integration branch merged
  once into `dev` via one maintainer-approved PR, instead of a PR per task. Requested by the
  maintainer during the M0 run.
