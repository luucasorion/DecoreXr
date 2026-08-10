# ADR 0010 — No-scene fallback via a common IPaintableSurface

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion (via grilling session)

## Context
MRUK needs the user to have run Space Setup. If the scene is empty or scene permission is
denied, there are no walls to paint. Options: guide-only, assume-exists, or guide + fallback.

## Decision
On start, check for a scene model; if missing/denied, **guide the user to Space Setup** AND
offer a **manual flat-plane fallback** (point at a surface and drop a plane anchor). Both an
MRUK wall anchor and a manual plane implement a common **`IPaintableSurface`** seam.

## Rationale
Robust for demos on other headsets, and the shared seam means the paint system never knows
where a surface came from — strengthening the decoupling in ADR 0008.

## Consequences
- Positive: works without a prior scan; one abstraction serves both surface origins.
- Negative / risks: manual-plane path is extra work and less accurate than Space Setup; treat
  as fallback, not the primary path.

## Alternatives Considered
- **Guide-only** — rejected: brittle when demoing on an un-scanned headset.
- **Assume scene exists** — rejected: fails silently for anyone but the author.

## Notes
`IPaintableSurface` lives in `Spatial`; painting depends on the interface, not the source.
