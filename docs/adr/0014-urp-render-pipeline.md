# ADR 0014 — URP as the render pipeline

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion (documented after the fact; pre-existing project state)

## Context
The project was created from the Unity 6 URP template (URP 17.3.0). This predates the grilling
session; it is recorded here because it is a real constraint on MR rendering, not a fresh choice.

## Decision
Keep **Universal Render Pipeline (URP)** as the render pipeline for DecoreXR.

## Rationale
URP is Meta-supported for Quest passthrough, is the current project state, and is the right
performance profile for a mobile XR GPU. Switching pipelines would be gratuitous churn.

## Consequences
- Positive: Meta-supported, mobile-appropriate, already configured.
- Negative / risks: passthrough + URP has specific requirements — correct render pipeline asset
  settings and **no fullscreen post-effects that occlude passthrough**. Must be handled
  deliberately during M0.

## Alternatives Considered
- **Built-in Render Pipeline** — rejected: legacy, weaker XR support.
- **HDRP** — rejected: not viable on mobile Quest hardware.

## Notes
PCVR mode (ADR 0012) still uses URP; quality scales via ADR 0004 budgets, not a pipeline swap.
