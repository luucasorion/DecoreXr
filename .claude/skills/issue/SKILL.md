---
name: issue
description: Resolve a DecoreXR task marker (e.g. "/issue m0 t7" or "/issue next") to its GitHub issue and run the standard dev-branch workflow — branch off dev, implement per the ADRs, open a PR into dev with "Closes #n". Use when the user invokes /issue.
---

# /issue — work a DecoreXR task

Invoked as `/issue m# t#`, `/issue M#-T#`, or `/issue next`.

## 0. Environment
- Windows / PowerShell. Call `gh` by full path if not on PATH: `/c/Program Files/GitHub CLI/gh`.
- For any `gh`/`git` **multi-line body**, write the body to a file and use `-F` / `--body-file`
  (inline `-m` and here-strings break on PowerShell).
- Honor the workflow in ADR 0015 and `CLAUDE.md`: `dev`-based, **one PR in flight at a time**,
  merges left to the maintainer.

## 1. Normalize the marker
- Lowercase/spacing tolerant: `m0 t7`, `M0 T7`, `m0-t7` → **`M0-T7`**.
- Reject markers that don't match `^M\d+-T\d+$` after normalization (except `next`).

## 2. Resolve `next` (if used)
1. Find the highest completed marker from **`dev` commit subjects only**:
   `git log origin/dev --format=%s` → parse leading `M#-T#` tokens, take the max.
2. The candidate is the **smallest open issue whose marker is greater** than that.
3. **Confirm the candidate with the user before doing any work.**

## 3. Resolve marker → GitHub issue
- Issue titles are `M#-T#: <summary>`. Find the open issue whose title starts with the marker:
  `gh issue list --state open --search "M0-T7 in:title" --json number,title`.
- If none/multiple match, report and stop (ask which).

## 4. Priority guard
- Respect a **high → medium → low** priority order via issue labels (`priority:high`, etc.).
  If an unblocked higher-priority issue exists, surface it and confirm before proceeding with a
  lower-priority marker.

## 5. Dependency check
- Read the task's dependencies from `docs/implementation-plan.md`. If a dependency issue is
  still open, warn and confirm before starting.

## 6. Standard workflow
1. Ensure a clean tree and sync: `git fetch origin`, `git checkout dev`, `git pull`.
2. **One PR in flight**: if an open PR authored for a prior task exists, stop — finish/await its
   merge first (do not stack).
3. Branch off `dev`: `git checkout -b <type>/M#-T#-<slug> dev` (`type` = feat/fix/chore).
4. Implement strictly per the ADRs and architecture §8. Stay within the task's scope.
5. Run the `adr-guardian` agent; resolve any BLOCKERs before opening the PR.
6. Commit; push `-u origin <branch>`.
7. Open a PR **into `dev`**. Write the body to a file including `Closes #<n>`, then:
   `gh pr create --base dev --title "M#-T#: <summary>" --body-file <file>`.
8. Set the PR's **milestone** and **project** to match the issue.
9. Report the PR URL. **Leave the merge to the maintainer**; issues are closed manually.

## Notes
- End commit messages with the required Co-Authored-By trailer.
- Never merge into `dev` or `main` yourself.
