# ADR 0012 — Keep a future PCVR mode possible

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion

## Context
Standalone Quest 3 is a mobile GPU sharing bandwidth with passthrough. The user wants a future
**PCVR mode** (via Meta Quest Link / OpenXR PC runtime) for more GPU power and higher quality.

## Decision
Target standalone-Android first, but **keep PCVR mode possible**: quality budgets (canvas
texels/m, texture cap, occlusion) stay **data-driven and per-platform switchable**, never
hard-coded to mobile limits.

## Rationale
The Meta XR SDK + MRUK choice (ADR 0001) already supports PCVR over Link, so this is a
constraint on how we express budgets, not a new SDK. It costs almost nothing now and preserves
a clear upgrade path.

## Consequences
- Positive: PCVR can raise quality via config only; no rework of the paint engine.
- Negative / risks: must resist scattering magic-number budgets through the code (enforced by
  ADR-guardian and architecture §8 "config-stays-config").

## Alternatives Considered
- **Standalone-only, hard-coded budgets** — rejected: would force a rewrite to add PCVR later.

## Notes
Not an MVP feature — a forward constraint on ADR 0004's budget config.
