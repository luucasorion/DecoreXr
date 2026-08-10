# ADR 0005 — Environment depth occlusion + z-offset

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion (via grilling session)

## Context
Virtual paint sits on the wall plane. Objects in front of the wall (hands, controller, people,
furniture) could incorrectly appear behind the paint. Paint can also z-fight against the wall.

## Decision
Enable **environment depth occlusion** (Meta Depth API) for the MVP, and apply a **~5mm forward
z-offset + depth bias** to the paint quad to prevent flicker.

## Rationale
Depth occlusion also makes the hand/controller correctly occlude paint, improving painting
feedback, not just realism. The z-offset removes coplanar flicker cheaply.

## Consequences
- Positive: correct occlusion of near objects; better painting feedback; no z-fighting.
- Negative / risks: depth occlusion costs GPU headroom and adds Depth API setup — a real budget
  concern on standalone Quest 3 (see ADR 0011 on-device verification).

## Alternatives Considered
- **Skip occlusion for MVP** — rejected by the user despite lower cost; feedback value won.

## Notes
Occlusion enable/disable is part of the data-driven budget (ADR 0004).
