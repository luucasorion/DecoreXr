# DecoreXR — Project Context & MVP Plan

> Mixed Reality interior decoration app for Meta Quest 3, built in Unity 6.
> This document captures the product vision, the locked technical decisions, and the
> plan for the wall-painting MVP. It is the shared source of truth for the project.

---

## 1. Product vision

DecoreXR lets users look at their **real environment** through the Quest 3 and design and
customize the space virtually **before making real changes**. Virtual decoration is placed in
the actual physical space and stays correctly aligned as the user moves.

Long-term capabilities:

- Change wall colors and finishes
- Create decorative patterns on walls
- Add and position furniture and decorative objects
- Change flooring and other surfaces
- Experiment with room layouts
- Save and compare decoration configurations

The real environment is the **foundation** of the experience. The architecture distinguishes
**surface-based decoration** (paint, in wall-UV space) from **3D spatial objects** (furniture,
placed by transform), so furniture can be added later without redesigning the painting system.

---

## 2. Current focus — Wall-painting MVP

The MVP validates the core technical loop:

> Real environment → spatial understanding → identify walls → select a wall →
> apply virtual decoration → maintain alignment while moving.

Painting is **not** limited to whole-wall single colors. The eventual goal is MS-Paint-style
tools adapted to real surfaces: full-wall fill, freehand brush, eraser, circles, rectangles,
triangles, diamonds, stripes/bands, custom patterns, multiple colors, adjustable sizes, undo/redo.

Example flows:
- Select Circle → point at wall → define size → choose color → place the shape.
- Select Stripe → define position and width → apply to the wall.

These behave as decoration **applied to the physical wall**, not floating 3D objects in front of it.

---

## 3. Environment (facts)

- **Unity:** 6000.3.20f1
- **Render pipeline:** URP 17.3.0 (URP template project)
- **Input System:** 1.19.0 present
- **XR stack:** none yet — Meta XR SDK / OpenXR / XR Plugin Management to be added
- **Platform target:** Quest 3 standalone (Android) first
- **VCS:** Git / GitHub

> ⚠️ URP + passthrough has specific setup requirements (render pipeline asset settings, no
> fullscreen post-effects that occlude passthrough). Handle deliberately.

---

## 4. Locked decisions

| Area | Decision | Rationale |
|---|---|---|
| **SDK** | Meta XR Core SDK + **MR Utility Kit (MRUK)** | MVP is "detect walls → select → stay aligned" — MRUK's core job. Vendor-neutrality not on roadmap. |
| **Input** | Controller ray **and** hand-pinch, behind one pointer interface | Controller precise for demo; hands drop in via the same seam. |
| **Paint model** | **Vector command-list = source of truth**, rendered into a per-wall texture (hybrid) | Every tool = a command type; makes undo/redo, save, and multi-wall nearly free. |
| **Canvas resolution** | 256 texels/meter, capped 1024×1024 per wall; **data-driven** | Balances quality vs mobile GPU/bandwidth; raisable for PCVR. |
| **Occlusion** | Environment **depth occlusion IN** (Meta Depth API) + ~5mm z-offset for flicker | Also makes hand/controller occlude paint → better painting feedback. |
| **Persistence** | Save/load **in MVP**; command lists keyed to MRUK **anchor UUID**, JSON in `persistentDataPath` | Command-list model makes this cheap; handle "anchor missing" on load. |
| **Undo/redo** | **Single global** command stack across all walls | Matches user expectation; extends to furniture actions later. |
| **Architecture** | 13 logical modules grouped into **~5 assemblies** | Boundaries preserved as namespaces/classes; avoids asmdef dependency-graph tax. |
| **Tool/color UI** | **Wrist-anchored** world-space uGUI palette | Standard MR pattern; always reachable; works for controller and hands. |
| **No-scene fallback** | Guide to Space Setup **+** manual flat-plane anchor; both feed one `IPaintableSurface` | Robust for demos on other headsets; strengthens the surface abstraction. |
| **Iteration** | Editor + Meta XR Simulator for logic/UI; periodic **on-device APK** for passthrough/depth/anchor truth | Passthrough, depth, anchors only behave correctly on real hardware. |

### Future goal — PCVR mode
A future **PCVR mode** (via Meta Quest Link / OpenXR PC runtime) is wanted for more GPU power and
higher quality. The SDK choice already supports it. **Keep quality budgets (texels/m, texture cap,
occlusion) data-driven / per-platform switchable — do not hard-code mobile limits.**

---

## 5. Architecture

### Assemblies (~5)

- **Core** — `IPaintCommand`, global undo stack, persistence, shared interfaces
- **Spatial** — MRUK wrapper: scene load, walls, raycast, `IPaintableSurface`
- **Interaction** — pointer sources (controller ray / hand pinch), wall selection
- **Painting** — paint model + command types + canvas renderer + tools
- **App** — bootstrapping, wrist-panel UI, wiring
- *(future)* **Furniture** — depends on Core + Spatial, decoupled from Painting

The 13 concepts you listed (spatial understanding, surfaces, selection, coordinates, decoration,
tools, paint data, colors/materials, 3D objects, furniture, interaction, undo/redo, persistence)
live as namespaces/classes **inside** these assemblies.

### Key seams (these keep furniture decoupled)

- **`IPaintableSurface`** — a paintable surface from an MRUK wall anchor *or* a manual plane; the
  paint system never knows the origin.
- **`IPaintCommand` + global command stack** — undo, persistence, and future furniture actions
  (Place/Move/Rotate/Scale) all share one history.
- **Wall-local `(u,v)` coordinate space** — the single abstraction every painting tool builds on.
  Ray hit → world → wall-local → normalized by wall width/height → `(u,v)` in `[0,1]`.

### Technical notes per concern

- **Scene understanding:** request `ScenePermission`; MRUK loads walls as `MRUKAnchor` (WALL_FACE)
  with pose + dimensions. Don't roll custom plane detection.
- **Wall selection:** one ray → intersect the `IPaintableSurface` plane + rectangle → hit = surface
  + `(u,v)`. Hit-testing goes through the seam rather than `MRUK.Raycast` so a manual fallback plane
  is selected by the same code as a wall, and MRUK stays inside `Spatial` (ADR 0010).
- **Paint mapping:** per-wall quad sized to the plane; material albedo = a texture; painting draws
  into it at `(u,v)`. Fill = clear to color; shapes = raster the shape; brush = interpolated polyline.
- **Alignment:** parent the paint quad to the wall's **spatial anchor**, never to the camera.
- **Freehand:** sample ray-wall hits over time in `(u,v)`, build a polyline, store as a Stroke
  command (points + width + color).
- **Undo/redo:** pop/push commands; re-render the affected wall.
- **Multiple surfaces:** one canvas per wall anchor, registry keyed by anchor UUID.

---

## 6. MVP milestones

1. Quest 3 passthrough working (URP-correct).
2. Detect walls via MRUK scene understanding (+ manual-plane fallback).
3. Select a wall (ray + raycast, hover highlight, trigger to select).
4. Apply a solid color to the selected wall (first command type).
5. Keep decoration aligned with the physical wall (spatial anchor parenting).
6. Add one geometric painting tool (circle).
7. Add multiple colors (wrist palette).
8. Add undo/redo (global stack). *(+ save/load, decided in-scope.)*

### First build increment (agreed)
**Spatial vertical slice first:** passthrough → MRUK scene (+ manual-plane fallback) →
ray-select a wall → **solid-color fill** → verify it stays anchored **on real hardware**.
This de-risks the least-controllable part (scene, anchors, alignment, passthrough) with minimal
code, and solid-fill is genuinely the first command type — not throwaway work.

**Sequencing after slice 1:** shape tool (circle) → color palette → freehand brush → undo/redo →
save/load. Each is a new command type or a UI addition — no redesign.

---

## 7. Out of scope for MVP (future milestones)

Furniture catalog, placing/moving/rotating/scaling 3D objects, collision/placement checks,
flooring and other surfaces, room-layout experiments, save/compare multiple full configurations,
custom patterns, and additional shape tools beyond the first.
