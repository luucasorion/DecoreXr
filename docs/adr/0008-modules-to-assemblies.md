# ADR 0008 — 13 logical modules grouped into ~5 assemblies

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion (via grilling session)

## Context
The user wants clear separation of ~13 concerns (spatial understanding, surfaces, selection,
coordinates, decoration, tools, paint data, colors/materials, 3D objects, furniture,
interaction, undo/redo, persistence) and decoupling so furniture can be added later.

## Decision
Keep all 13 concerns as **namespaces/classes**, grouped into **~5 assembly definitions**:
`Core`, `Spatial`, `Interaction`, `Painting`, `App` (furniture arrives later as its own
assembly depending on `Core` + `Spatial`).

## Rationale
Coupling is reduced by clean interfaces, not by folder/asmdef count. Five assemblies preserve
the exact seams (paint-vs-3D, spatial-vs-decoration) as compile-time boundaries while avoiding
the asmdef dependency-graph tax and slow compiles of 13 separate assemblies on a solo project.

## Consequences
- Positive: real enforced boundaries with fast iteration; furniture stays decoupled.
- Negative / risks: discipline needed to keep concerns from bleeding across namespaces within
  an assembly (enforced by the ADR-guardian review).

## Alternatives Considered
- **13 separate assemblies** — rejected: dependency-ordering pain, slower compiles, no coupling
  benefit over interfaces.
- **Single assembly, folders only** — rejected: boundaries not enforced at compile time.

## Notes
Key seams: `IPaintableSurface`, `IPaintCommand` + global stack, wall-local `(u,v)` space.
