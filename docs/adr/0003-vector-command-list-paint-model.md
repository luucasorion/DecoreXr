# ADR 0003 — Vector command-list paint model rendered into a per-wall texture

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion (via grilling session)

## Context
Painting is not limited to whole-wall fills; the goal is MS-Paint-style tools (fill, brush,
eraser, circle, rectangle, triangle, diamond, stripe, patterns) on real walls. We need a
source-of-truth representation for what has been painted.

## Decision
Use a **vector command list as the source of truth**, rendered into a per-wall texture for
display. Each tool is a command type implementing a shared command interface.

## Rationale
This unifies every tool as a 2D operation in wall-UV space, and makes undo/redo (ADR 0007),
persistence (ADR 0006), and multiple decorated walls nearly free. Adding a tool = adding a
command type, with no redesign.

## Consequences
- Positive: cheap undo/persistence; resolution-independent source; trivial multi-wall support.
- Negative / risks: more upfront work than raw raster; a renderer must translate commands →
  texture. Re-rendering a wall on each change must stay within budget (ADR 0004).

## Alternatives Considered
- **Raster canvas (draw pixels directly)** — rejected: undo needs snapshots, resolution-locked.
- **Floating meshes per shape** — rejected by product intent: paint must live *on* the wall,
  not as floating 3D objects.

## Notes
Wall-local `(u,v)` in `[0,1]` (from ray hit ÷ wall dimensions) is the coordinate space all
commands use.
