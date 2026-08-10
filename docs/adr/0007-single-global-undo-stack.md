# ADR 0007 — Single global undo/redo command stack

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion (via grilling session)

## Context
Paint operations are commands (ADR 0003) across potentially many walls. Undo can be scoped
per-wall or globally.

## Decision
Use a **single global command stack** across all walls (and, later, all decoration including
furniture). Each command carries its target's identity (e.g. wall anchor UUID).

## Rationale
Matches user expectation — "undo undoes the last thing I did," regardless of wall. It is also
the natural shape for one unified room-wide action history when furniture arrives.

## Consequences
- Positive: intuitive; one history extends to future decoration types.
- Negative / risks: undo may jump the user's focus to a different wall; the UI should indicate
  what was undone.

## Alternatives Considered
- **Per-wall stacks** — rejected: users don't track "which wall's history am I in."

## Notes
The command interface + stack live in `Core`; re-render on undo/redo targets only the affected
wall.
