# ADR 0009 — Wrist-anchored uGUI tool/color palette

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion (via grilling session)

## Context
The user needs to pick a tool (fill, circle, brush…) and a color in MR. UI can be
hand/wrist-anchored, a fixed floating panel, or a controller radial menu.

## Decision
Use a **hand/wrist-anchored world-space uGUI palette** that follows the off-hand.

## Rationale
Standard MR pattern: always reachable, doesn't clutter the room, and works whether the pointing
hand is a controller or a tracked hand (ADR 0002). World-space uGUI keeps it simple to build.

## Consequences
- Positive: reachable, uncluttered, scales as the tool set grows.
- Negative / risks: wrist tracking jitter can affect readability; needs a comfortable offset.

## Alternatives Considered
- **Fixed floating panel** — rejected: makes users chase a panel.
- **Controller radial menu** — rejected: hides options, doesn't scale to a growing tool set.

## Notes
UI lives in `App`; it commands `Painting`/`Interaction` through their public interfaces only.
