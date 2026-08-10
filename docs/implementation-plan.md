# DecoreXR — Implementation Plan

Milestones are shippable/verifiable increments, dependency-ordered. Tasks (`M#-T#`) are sized
to become single GitHub issues (titles: `M#-T#: <summary>`). See
[`project-context.md`](project-context.md), [`architecture.md`](architecture.md), and
[`adr/`](adr/). No work starts before the ADR gate is satisfied (architecture §8).

**Agreed first increment (spans M0→M3):** passthrough → MRUK scene (+ manual-plane fallback) →
ray-select a wall → **solid-color fill** → verify it stays anchored **on real hardware**.

---

## M0 — Foundation & device setup
Assembly defs, passthrough scene via Meta XR MCP, OVRManager/permissions/manifest, first
on-device build, repo conventions.

| Task | Description | Dependencies |
|------|-------------|--------------|
| M0-T1 | Create assembly definitions `Core`, `Spatial`, `Interaction`, `Painting`, `App` with one-way references (architecture §4) | — |
| M0-T2 | Install/configure Meta XR via the Meta XR MCP Extension: camera rig + passthrough Building Block | — |
| M0-T3 | Android manifest + scene/depth permissions + OVRManager settings + URP passthrough render settings (ADR 0014) | M0-T2 |
| M0-T4 | Passthrough sanity scene + first on-device APK build on Quest 3 | M0-T3 |
| M0-T5 | Repo conventions: quality-budget config asset stub (ADR 0004/0012), Git LFS/`.gitignore` check, doc pointers | M0-T1 |

## M1 — Scene understanding & paintable surfaces
| Task | Description | Dependencies |
|------|-------------|--------------|
| M1-T1 | `Spatial`: request scene permission + load the MRUK scene model | M0-T4 |
| M1-T2 | `Spatial`: expose MRUK walls as `IPaintableSurface` (pose, dimensions, `(u,v)` mapping) (ADR 0001/0010) | M1-T1 |
| M1-T3 | `Spatial`/`App`: no-scene handling — guide user to Space Setup + reload (ADR 0010) | M1-T1 |
| M1-T4 | `Spatial`: manual flat-plane fallback anchor implementing `IPaintableSurface` | M1-T2, M1-T3 |

## M2 — Wall selection & input
| Task | Description | Dependencies |
|------|-------------|--------------|
| M2-T1 | `Interaction`: `IPointerSource` + controller-ray implementation (ADR 0002) | M0-T4 |
| M2-T2 | `Interaction`: hand-pinch `IPointerSource` implementation | M2-T1 |
| M2-T3 | `Interaction`: raycast against `IPaintableSurface` → surface + wall-local `(u,v)` | M1-T2, M2-T1 |
| M2-T4 | `Interaction`/`App`: hover highlight + select-on-trigger | M2-T3 |

## M3 — Solid-color fill (vertical-slice completion)
| Task | Description | Dependencies |
|------|-------------|--------------|
| M3-T1 | `Core`: `IPaintCommand` + single global command stack (push) (ADR 0003/0007) | M0-T1 |
| M3-T2 | `Painting`: per-wall canvas texture + anchored quad with ~5mm z-offset, sized to surface (ADR 0003/0004/0005) | M1-T2, M0-T1 |
| M3-T3 | `Painting`: `FillCommand` (whole-surface solid color) + renderer | M3-T1, M3-T2 |
| M3-T4 | `App`: wire select→fill; **on-device** verify paint stays anchored while moving (ADR 0011) | M2-T4, M3-T3 |

## M4 — Depth occlusion & alignment hardening
| Task | Description | Dependencies |
|------|-------------|--------------|
| M4-T1 | Enable environment depth occlusion (Depth API / Occlusion Building Block) (ADR 0005) | M3-T4 |
| M4-T2 | On-device occlusion + alignment verification; tune z-offset/depth bias | M4-T1 |

## M5 — Geometric tool, colors & palette UI
| Task | Description | Dependencies |
|------|-------------|--------------|
| M5-T1 | `App`: wrist-anchored uGUI palette scaffold + tool/color selection state (ADR 0009) | M3-T4 |
| M5-T2 | `Painting`: `CircleCommand` (`(u,v)` center + radius) + renderer | M3-T3 |
| M5-T3 | `Painting`/`App`: color model + multiple colors wired to the palette | M5-T1, M3-T3 |
| M5-T4 | `Interaction`: size/radius definition gesture for shapes | M5-T2, M2-T4 |

## M6 — Freehand brush & eraser
| Task | Description | Dependencies |
|------|-------------|--------------|
| M6-T1 | `Painting`: `StrokeCommand` (polyline in `(u,v)`, width, color) with sampling/interpolation | M5-T4 |
| M6-T2 | `Painting`: `EraserCommand` | M6-T1 |

## M7 — Undo/redo
| Task | Description | Dependencies |
|------|-------------|--------------|
| M7-T1 | `Core`: redo support + re-render only the affected wall on undo/redo (ADR 0007) | M3-T1 |
| M7-T2 | `App`: undo/redo palette controls + indication of what was undone | M7-T1, M5-T1 |

## M8 — Persistence
| Task | Description | Dependencies |
|------|-------------|--------------|
| M8-T1 | `Core`: serialize/deserialize command lists to JSON in `persistentDataPath` keyed by anchor UUID (ADR 0006) | M3-T1 |
| M8-T2 | `Core`/`Spatial`: reattach paint on load; skip missing anchors (clean fail — architecture §8) | M8-T1, M1-T2 |
| M8-T3 | `App`: save/load triggers + on-device restart verification | M8-T2 |

---

## Critical path

```
M0-T1 → M0-T2 → M0-T3 → M0-T4 → M1-T1 → M1-T2 → M2-T3 → M2-T4
      → M3-T2 → M3-T3 → M3-T4 → M4-T1 → M4-T2 → M5-T1 → M5-T4
      → M6-T1 → M7-T1 → M7-T2 → M8-T1 → M8-T2 → M8-T3
```

M0 gates everything. The MVP "definition of done" is M8-T3 (all 8 product milestones: passthrough,
wall detection, selection, solid fill, alignment, one geometric tool, multiple colors, undo/redo,
plus in-scope save/load). Furniture and further tools are post-MVP (out of scope, ADR 0008).
