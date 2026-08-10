# ADR 0004 — Canvas resolution 256 texels/m, cap 1024², data-driven budgets

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion (via grilling session)

## Context
Each painted wall gets a texture (ADR 0003). Resolution trades visual quality against mobile
GPU memory and fill-rate, which is shared with passthrough on Quest 3.

## Decision
Default to **256 texels per meter**, **capped at 1024×1024 per wall**. Express the budget
(texels/m, cap, occlusion toggles) as **data-driven, per-platform settings** — not constants.

## Rationale
256 px/m keeps a typical wall well under 1K² and looks clean for shapes and stripes while
leaving headroom for several walls. Data-driven budgets let the future PCVR mode (ADR 0012)
raise quality without code changes.

## Consequences
- Positive: predictable memory/fill-rate; PCVR can scale up via config only.
- Negative / risks: oversized walls clamp to the cap and lose density; fine freehand detail is
  limited at 256 px/m.

## Alternatives Considered
- **128 px/m** — rejected: too blurry for fine brush work.
- **512 px/m** — rejected for standalone: memory/fill-rate too high across multiple walls.

## Notes
Budgets belong in a config asset, not hard-coded (see architecture §8 "config-stays-config").
