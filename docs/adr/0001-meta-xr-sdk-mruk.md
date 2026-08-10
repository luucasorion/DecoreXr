# ADR 0001 — Meta XR SDK + MR Utility Kit (MRUK)

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion (via grilling session)

## Context
DecoreXR must detect a room's walls, let the user select one, and keep virtual paint aligned
to the physical wall on Quest 3. Two paths exist: Meta's own SDK stack, or a vendor-neutral
OpenXR + XR Interaction Toolkit stack.

## Decision
Use the **Meta XR Core SDK + MR Utility Kit (MRUK)** for passthrough, scene understanding,
walls, planes, and raycasts.

## Rationale
The MVP is literally "detect walls → select → stay aligned," which is MRUK's core job. MRUK
exposes walls as `MRUKAnchor` (WALL_FACE) objects with pose + dimensions and built-in raycasts.
Vendor neutrality is not on the roadmap.

## Consequences
- Positive: least code for scene/wall/alignment; best docs and samples for scene understanding.
- Negative / risks: coupling to Meta's SDK; portability to non-Meta headsets is sacrificed.
  Mitigated by wrapping MRUK behind our own `Spatial` assembly interfaces (see ADR 0008/0010).

## Alternatives Considered
- **OpenXR + XR Interaction Toolkit** — rejected: portability isn't needed, and scene
  understanding is thinner, forcing us to hand-roll what MRUK gives for free.

## Notes
Standalone-Android first; also runs over Meta Quest Link for the future PCVR mode (ADR 0012).
