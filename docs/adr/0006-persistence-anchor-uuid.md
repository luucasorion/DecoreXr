# ADR 0006 — Persistence in the MVP, keyed to anchor UUID

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion (via grilling session)

## Context
The user wants painted walls to survive app restarts as part of the MVP (not deferred).
Paint is a vector command list (ADR 0003); walls are MRUK anchors with stable UUIDs.

## Decision
**Save/load in the MVP.** Serialize each wall's command list as JSON in
`Application.persistentDataPath`, keyed by the wall's **MRUK anchor UUID**.

## Rationale
The command-list model makes persistence cheap (small, JSON-friendly data). Anchor UUIDs are
stable across sessions as long as the room isn't re-captured in Space Setup, giving a reliable
key to re-attach paint.

## Consequences
- Positive: paint restores automatically; tiny save files; forward-compatible with furniture.
- Negative / risks: re-running Space Setup changes anchors → orphaned data. Must handle
  "anchor no longer exists" on load (fail to a clean state, not a crash — architecture §8).

## Alternatives Considered
- **Defer persistence to post-MVP** — rejected by the user; wanted it in-scope.

## Notes
Serialization lives in `Core`; anchor UUID lookup in `Spatial` (ADR 0008).
