---
name: milestone
description: Drive a whole DecoreXR milestone to completion, one task at a time. Like /issue but for every task in a milestone (M#) — resolves the milestone's issues, orders them by dependency, and runs the standard dev-branch workflow task-by-task, honoring "one PR in flight at a time". Use when the user invokes /milestone.
---

# /milestone — work an entire DecoreXR milestone

Invoked as `/milestone m#`, `/milestone M#`, or `/milestone next`.

This is a **driver over the `/issue` workflow**: it does not invent a new process. For each
task it runs exactly the `.claude/skills/issue/SKILL.md` steps (branch off `dev` → implement per
ADRs → `adr-guardian` → PR into `dev` with `Closes #n` → set milestone + project). It never
merges — the maintainer does.

## 0. Environment (same as /issue)
- Windows / PowerShell. Call `gh` by full path if not on PATH: `/c/Program Files/GitHub CLI/gh`.
- Multi-line `gh`/`git` bodies → write to a file and use `-F` / `--body-file`.
- Honor ADR 0015 + `CLAUDE.md`: `dev`-based, **one PR in flight at a time**, merges left to the
  maintainer. This is the rule that makes a milestone run **serial**, not parallel.

## 1. Normalize the marker
- Tolerant of case/spacing: `m0`, `M 0`, `M0` → **`M0`**. Validate `^M\d+$` (or `next`).

## 2. Resolve `next` (if used)
1. Highest completed task from **`dev` commit subjects only**: `git log origin/dev --format=%s`
   → parse leading `M#-T#` tokens.
2. The candidate milestone is the **lowest-numbered milestone that still has open issues**.
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
   (the dependency's PR should be merged first).
4. **Priority guard**: within the ordering, respect `priority:high → medium → low` labels; if a
   higher-priority unblocked task exists, do it first.

## 5. Determine progress / resume point
- A task is **done** when its `M#-T#` marker appears in `dev` commit subjects (its PR merged).
- Skip done tasks. The **current task** is the first not-done task in the order from §4.
- Report: `Milestone M#: k/N tasks complete. Next: M#-T#`.

## 6. The serial loop (core behavior)
Repeat for the current task, **one at a time**:

1. **One-PR-in-flight gate.** If there is an **open PR** from a previous task in this run, STOP:
   report that the run is paused waiting for the maintainer to merge it. Do **not** branch or
   open a second PR. (Re-running `/milestone m#` later resumes from §5.)
2. Sync: `git fetch origin`, ensure a clean tree, base off latest `origin/dev`.
3. Run the **`/issue` workflow** for the current task marker (see `issue` skill): branch off
   `dev`, implement strictly per the ADRs and architecture §8, stay in the task's scope.
4. Run the `adr-guardian` agent; resolve all **BLOCKER**s before opening the PR.
5. Open the PR into `dev` (`Closes #n`, milestone + project set), push, report the PR URL.
6. **Pause.** Announce: "M#-T# PR opened — waiting for merge before the next task." Then either:
   - if the user wants unattended progress, poll `dev` commit subjects for the task's marker
     before advancing; **or**
   - stop and let the user re-invoke `/milestone m#` after merging (default — respects the
     one-PR rule without long polling).

## 7. Completion
- When every task's marker for the milestone is in `dev` commit subjects, report the milestone
  complete and list the merged tasks.
- Issues are **closed manually** by the maintainer (a `dev` merge does not auto-close). Offer to
  close the milestone's issues, but do not close them without the maintainer's go-ahead.

## Guardrails
- Never open more than one PR per run before a merge. Never merge into `dev`/`main`.
- Never skip the `adr-guardian` check or the ADR gate.
- End commit messages with the required Co-Authored-By trailer.
- If the milestone marker is unknown or has no issues, stop and report — do not guess.
