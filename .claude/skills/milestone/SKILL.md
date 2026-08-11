---
name: milestone
description: Drive a whole DecoreXR milestone to completion on a single per-milestone integration branch. Resolves the milestone's issues, orders them by dependency, and commits each task to one `M#` branch (each task `adr-guardian`-reviewed), landing the whole milestone via one maintainer-approved PR `M# → dev`. Use when the user invokes /milestone.
---

# /milestone — work an entire DecoreXR milestone on one integration branch

Invoked as `/milestone m#`, `/milestone M#`, or `/milestone next`.

**Milestone rule (ADR 0015).** A milestone run does **not** open a PR per task. It creates one
integration branch **`M#` off `dev`**, commits each task to `M#`, and lands the whole milestone
via **one maintainer-approved PR `M# → dev`** at the end. Per task it still runs the core
`/issue` implementation steps (implement per ADRs → `adr-guardian` → commit with `Closes #n` info
captured for the final PR). It never merges — the maintainer does.

## 0. Environment
- Windows / PowerShell. Call `gh` by full path if not on PATH: `/c/Program Files/GitHub CLI/gh`.
- Multi-line `gh`/`git` bodies → write to a file and use `-F` / `--body-file`.
- Honor ADR 0015 + `CLAUDE.md`: **per-milestone integration branch, one `M# → dev` PR**, merges
  left to the maintainer. Serial within the milestone; the ADR gate and `adr-guardian` still apply
  to every task.

## 1. Normalize the marker
- Tolerant of case/spacing: `m0`, `M 0`, `M0` → **`M0`**. Validate `^M\d+$` (or `next`).

## 2. Resolve `next` (if used)
1. Highest fully-merged milestone from **`dev` commit subjects**: `git log origin/dev --format=%s`
   → parse leading `M#-T#` tokens.
2. The candidate milestone is the **lowest-numbered milestone that still has open issues**
   (account for a milestone that may already be in progress on its own `M#` branch).
3. **Confirm the candidate milestone with the user before doing any work.**

## 3. Resolve the milestone → its tasks
- Find the milestone's open issues by title prefix (robust against the exact milestone title):
  `gh issue list --state open --search "M0-T in:title" --json number,title,labels --limit 100`.
- If the milestone has **no open issues**, report it as already complete and stop.

## 4. Order the tasks
1. Read `docs/implementation-plan.md` for each task's **dependencies**.
2. Topologically order the milestone's open tasks; break ties by ascending `T#`.
3. **Cross-milestone dependency guard**: if a task depends on a task in an *earlier* milestone
   whose marker is **not** yet in `dev` commit subjects, warn and confirm before proceeding
   (the earlier milestone should be merged first).
4. **Priority guard**: within the ordering, respect `priority:high → medium → low` labels; if a
   higher-priority unblocked task exists, do it first.

## 5. Determine progress / resume point
- A task is **done** when its `M#-T#` marker appears in the **`M#` branch** commit subjects
  (`git log origin/M# --format=%s`, or local `M#` if not yet pushed). Once the milestone's
  `M# → dev` PR merges, the markers also land in `dev`.
- Skip done tasks. The **current task** is the first not-done task in the order from §4.
- Report: `Milestone M#: k/N tasks complete on branch M#. Next: M#-T#`.

## 6. The serial loop (core behavior)
Set up the integration branch once, then work tasks one at a time onto it.

**Setup (once per run):** `git fetch origin`; ensure a clean tree. If branch `M#` does not exist
(local or `origin/M#`), create it off latest `origin/dev`: `git checkout -b M# origin/dev`.
Otherwise check it out and fast-forward to `origin/M#`. Confirm the integration-branch mode with
the user on the first run of a milestone (per the rule; don't silently assume for edge cases).

Repeat for the current task, **one at a time**:
1. **Milestone-PR gate.** If the `M# → dev` PR is already **open** (milestone finished/paused for
   review), STOP: report it's waiting on the maintainer. Do not open a second PR.
2. Ensure you are on `M#` with a clean tree (commit or stash unrelated editor churn; keep it out
   of task commits).
3. Run the core **`/issue` implementation steps** for the current task marker: implement strictly
   per the ADRs and architecture §8, staying in the task's scope. Do **not** branch per task and
   do **not** open a per-task PR — work directly on `M#`.
4. Run the `adr-guardian` agent; resolve all **BLOCKER**s before committing.
5. **Commit to `M#`** with the `M#-T#` marker leading the subject and the Co-Authored-By trailer;
   note the task's issue number for the final PR's `Closes #n` list. Push `M#`.
6. Advance to the next task in the §4 order and repeat from step 1. (No pause between tasks — the
   serialization is within the single branch, not per-PR.)

## 7. Completion → the single PR
- When every task's marker is on `M#` (or the runnable subset is done and remaining tasks are
  **deferred**, e.g. on-device tasks per ADR 0011), open **one** PR `M# → dev`:
  `gh pr create --base dev --head M# --title "M# — <milestone name>" --body-file <file>`.
- PR body: summarize each task + status, list `Closes #n` for the completed tasks, call out any
  **deferred** tasks and why. Set the PR's **milestone + project**. Report the PR URL.
- **Leave the merge to the maintainer.** Issues are **closed manually** (a `dev` merge does not
  auto-close). Offer to close the milestone's completed issues, but don't without the go-ahead.
- Re-running `/milestone m#` after more work resumes from §5 against the same `M#` branch.

## Guardrails
- **One integration branch per milestone; one `M# → dev` PR.** Never open a per-task PR in a
  milestone run, and never more than one milestone PR before its merge. Never merge into
  `dev`/`main`.
- Never skip the `adr-guardian` check or the ADR gate — every task is reviewed before its commit.
- End commit messages with the required Co-Authored-By trailer.
- If the milestone marker is unknown or has no issues, stop and report — do not guess.
