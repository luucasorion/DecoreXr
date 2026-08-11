# CLAUDE.md — DecoreXR

DecoreXR is a Unity 6 / Meta Quest 3 **Mixed Reality interior decoration** app. The MVP is
**wall painting**; furniture and other decoration are future milestones.

## Read these before proposing or making any change (docs win over memory)
- [`docs/project-context.md`](docs/project-context.md) — vision, scope, locked decisions.
- [`docs/architecture.md`](docs/architecture.md) — components, data flow, **§7 constraints**,
  **§8 development conventions**, traceability.
- [`docs/adr/`](docs/adr/) — the numbered Architecture Decision Records (0001–0015).
- [`docs/implementation-plan.md`](docs/implementation-plan.md) — milestones `M0–M8`, tasks `M#-T#`.

## The ADR gate (non-negotiable)
No new technology, package, dependency, or cross-cutting architectural decision without a
matching ADR in `docs/adr/`. If a change needs one, **propose the ADR and ask** before writing
code. Introducing one without an ADR is a BLOCKER (see architecture §8.1). The `adr-guardian`
agent enforces this and the rest of §8.

## Git / branching workflow (ADR 0015)
- `main` = stable; **`dev` = integration branch**.
- Every change **branches off `dev`**, opens a **PR into `dev`** whose body contains
  `Closes #n`, and is **merged by the maintainer** (lucas.orion) — never by Claude.
- **One PR in flight at a time.** Finish it, wait for the merge, then branch the next off `dev`.
  **Do not stack PRs.**
- Set each PR's **milestone + project** to match its issue. Issues are **closed manually** (a
  `dev` merge does not auto-close them).
- **Milestone rule (ADR 0015):** a `/milestone m#` run does **not** PR per task. It creates one
  integration branch **`M#` off `dev`**, commits each task to `M#` (each still `adr-guardian`-
  reviewed, `M#-T#` in the commit subject), and lands the milestone via **one maintainer PR
  `M# → dev`** at the end. A task is "done" when its marker is in `M#` commit subjects; on-device
  tasks (ADR 0011) may be deferred and the PR opened for the runnable subset.
- Use the `/issue m# t#` (or `/issue next`) skill for a single task (per-task PR into `dev`), or
  `/milestone m#` (or `/milestone next`) for a whole milestone (integration branch → one PR).

## Environment / conventions
- OS is **Windows**, shell is **PowerShell**. For `git`/`gh` **multi-line bodies**, write the
  body to a file and use `-F` / `--body-file` (inline `-m` / here-strings break).
- Call `gh` by full path if not on PATH: `/c/Program Files/GitHub CLI/gh`.
- Meta-XR Editor setup (rig, passthrough, Android manifest, permissions, MRUK, Interaction SDK)
  goes through the **coplayDev Unity MCP** at `http://127.0.0.1:8080` (ADR 0013) — via
  `execute_code` + `BlockData.ContextMenuInstall()` — not hand-config. The Meta XR MCP Extension
  is optional/supplementary. Confirm the endpoint (HTTP port **8080**) before any Unity MCP call.
- Persist non-obvious facts to Claude Code memory with a one-line pointer in `MEMORY.md`.

## Scope discipline
MVP scope = the milestone list in the implementation plan, nothing more. Furniture / non-MVP
decoration must not leak into the `Painting` assembly (ADR 0008, architecture §8.7).
