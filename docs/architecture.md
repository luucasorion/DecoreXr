# DecoreXR — Architecture

How the system is organized to satisfy [`project-context.md`](project-context.md) and the
ADRs in [`adr/`](adr/). This document introduces **no new technology or decision**. Where it
names a component boundary that is not itself a recorded decision, it is labelled an
*organizational choice*.

---

## 1. System Overview

DecoreXR is a Quest 3 Mixed Reality app. The real room is captured by Meta's scene system;
walls become paintable surfaces; the user points with a controller or hand and applies paint
commands that are stored as a vector list and rendered into per-wall textures anchored to the
physical walls. All decoration actions flow through one command history and can be saved.

```
                 +-------------------+
   Space Setup   |  Meta XR / MRUK   |   passthrough + scene + depth
   (on device) ->|  (Spatial)        |----------------------------------+
                 +-------------------+                                   |
                          | walls as IPaintableSurface                  | depth occlusion
                          v                                             v
 controller/hands   +-----------+   ray hit (u,v)   +--------------+   +-----------------+
 (Interaction) ---->| Selection |----------------->|  Painting    |-->| Per-wall texture |
   IPointerSource   +-----------+                  |  (tools ->    |   | on anchored quad |
                          |                        |   IPaintCommand)  +-----------------+
                          |  selected surface      +--------------+           ^
                          v                               |                   | render
                    +-----------+   push/undo/redo        v                   |
   wrist palette -->|   App/UI  |------------------> +-----------------+      |
   (App)            +-----------+                    |  Core: command  |------+
                                                     |  stack + persist|
                                                     +-----------------+
                                                              |
                                                     JSON in persistentDataPath
                                                     (keyed by anchor UUID)
```

---

## 2. Main Components

| # | Component (assembly) | Nature / source |
|---|----------------------|-----------------|
| 1 | `Core` | Decision — ADR 0008 (assemblies), ADR 0003/0007 (commands/undo), ADR 0006 (persistence) |
| 2 | `Spatial` | Decision — ADR 0001 (MRUK), ADR 0010 (`IPaintableSurface`), ADR 0006 (anchor UUID) |
| 3 | `Interaction` | Decision — ADR 0002 (`IPointerSource`), plus selection (*organizational choice*) |
| 4 | `Painting` | Decision — ADR 0003 (paint model + renderer + tools), ADR 0004 (canvas budget) |
| 5 | `App` | Decision — ADR 0009 (wrist palette); bootstrapping/wiring (*organizational choice*) |
| 6 | `Furniture` *(future)* | Decision — ADR 0008 (own assembly, depends on `Core`+`Spatial`); not in MVP |
| — | URP render pipeline | Decision — ADR 0014 |
| — | Meta XR SDK / MRUK / Depth API | Decision — ADR 0001, ADR 0005 |
| — | Unity MCP + Meta XR MCP Extension | Decision — ADR 0013 (editor/tooling only) |

---

## 3. Responsibilities of Each Component

- **Core** — defines `IPaintCommand`, the single global undo/redo stack (ADR 0007), the
  serializer that reads/writes command lists as JSON keyed by anchor UUID (ADR 0006), and
  shared interfaces/value types (e.g. wall-local `(u,v)`). Knows nothing about MRUK or UI.
- **Spatial** — wraps MRUK: requests scene permission, loads walls as `MRUKAnchor`, exposes
  them (and manual fallback planes) as `IPaintableSurface` with its plane, rectangle and `(u,v)`
  mapping, and resolves anchor UUIDs. The only assembly that references Meta scene APIs.
- **Interaction** — provides `IPointerSource` implementations for controller ray and hand
  pinch (ADR 0002) with pen-down/up events, intersects a pointer ray with the `IPaintableSurface`
  plane + rectangle, and turns the nearest hit into a selected surface and a `(u,v)`. Hit-testing
  lives here rather than in `Spatial` so walls and manual fallback planes are selected by one piece
  of code and no MRUK type crosses the seam (ADR 0010, §8.2).
  *Selection is an organizational grouping within this assembly.*
  The two sources are arbitrated behind one `IPointerSource` so nothing downstream asks which
  is live: a source that is mid-press keeps the pointer until it releases (a stroke is never
  handed to another input halfway through), an already-pressed source outranks a merely active
  one, and otherwise the first usable source in configured order wins — controller first, as
  ADR 0002 makes it the primary path. *The arbitration rules are an organizational choice.*
- **Painting** — the paint model: command types (fill, brush, circle, rectangle, triangle,
  diamond, stripe, …), the renderer that draws a command list into a per-wall texture within
  the data-driven budget (ADR 0004), and the per-wall canvas registry keyed by anchor UUID.
- **App** — bootstraps the scene, builds the wrist-anchored uGUI palette (ADR 0009), and wires
  Interaction → Painting → Core through their public interfaces. Owns tool/color selection state.
- **Furniture (future)** — 3D `SpatialObject` placement/move/rotate/scale as new
  `IPaintCommand`s sharing the global stack; depends on `Core` + `Spatial`, never on `Painting`.

---

## 4. How Components Communicate

- **Interfaces, one direction.** `App` depends on `Painting`/`Interaction`/`Spatial`/`Core`;
  `Painting` depends on `Core` (+ `IPaintableSurface` from `Spatial`); `Spatial`,
  `Interaction`, `Core` do **not** depend on `App` or `Painting`. No cyclic asmdef references.
- **Commands as the currency.** User actions become `IPaintCommand` instances pushed onto the
  Core stack; undo/redo pops/re-pushes and asks `Painting` to re-render only the affected wall.
- **Surface abstraction.** `Painting` and `Interaction` consume `IPaintableSurface`; they never
  reference MRUK types directly (ADR 0010).
- **No hidden singletons across boundaries** beyond a single explicit composition root in `App`.

---

## 5. External Dependencies

**Runtime**
- Meta XR Core SDK + MR Utility Kit (MRUK) — scene, walls, raycasts (ADR 0001)
- Meta Depth API — environment occlusion (ADR 0005)
- Universal Render Pipeline 17.3 (ADR 0014)
- Meta XR Core SDK input — `OVRInput` (controller) and `OVRHand` (pinch) feed the `IPointerSource`
  implementations (ADR 0001, ADR 0002); it reads through Unity Input System 1.19 underneath

**Editor / tooling (not shipped in the build)**
- Unity MCP `com.coplaydev.unity-mcp` (ADR 0013)
- Meta XR Unity MCP Extension `com.meta.xr.unity-mcp.extension` (ADR 0013)
- Meta XR Simulator — Editor iteration (ADR 0011)
- Git / GitHub + `gh` CLI — workflow (ADR 0015)

---

## 6. Data Flow (end to end)

1. On launch, `Spatial` requests scene permission and loads the scene model (ADR 0010).
2. If no scene/permission, `App` guides the user to Space Setup and/or offers a manual plane;
   either way `Spatial` yields `IPaintableSurface`s.
3. `Interaction` produces a ray from the active `IPointerSource` (controller or hand).
4. The ray raycasts against surfaces; a hit yields the surface + wall-local `(u,v)`.
5. `App` combines the current tool + color + `(u,v)` into a concrete `IPaintCommand`.
6. `Core` pushes the command onto the global stack.
7. `Painting` applies the command to that wall's canvas and re-renders its texture within budget.
8. The texture displays on the anchored quad (~5mm z-offset), depth-occluded by near objects.
9. Undo/redo pops/re-pushes on the Core stack; `Painting` re-renders the affected wall.
10. On save, `Core` serializes each wall's command list to JSON keyed by anchor UUID; on load,
    it restores them, skipping any anchor that no longer exists (clean fail).

---

## 7. Important Technical Constraints

- **URP + passthrough**: correct render pipeline asset settings; no fullscreen post-effects that
  occlude passthrough (ADR 0014).
- **Mobile GPU budget**: canvas 256 texels/m, cap 1024² per wall; occlusion costs headroom
  (ADR 0004, 0005). Target the device refresh rate (72/90 Hz); avoid per-frame texture realloc.
- **Anchor stability**: alignment depends on parenting to the wall's spatial anchor, never the
  camera; re-running Space Setup invalidates anchor UUIDs (ADR 0006).
- **On-device truth**: passthrough, depth, alignment, and anchor persistence are only trustworthy
  on Quest 3 (ADR 0011).
- **PCVR forward-constraint**: budgets stay data-driven/per-platform (ADR 0012).

---

## 8. Development Conventions (enforceable rules)

1. **ADR gate** — no new technology, dependency, or cross-cutting decision without a
   corresponding ADR in `docs/adr/`. Code that introduces one without an ADR is a BLOCKER.
2. **Isolation** — assembly dependencies flow one way (§4). No cyclic references; `Spatial`,
   `Interaction`, `Core` never depend on `App`/`Painting`. MRUK types stay inside `Spatial`.
3. **Config-stays-config** — quality budgets (texels/m, texture cap, occlusion toggles) live in
   a config asset, not as magic numbers in code (ADR 0004, 0012). Concrete home:
   `DecoreXR.Core.QualityBudgetConfig` (`Assets/DecoreXR/Core/QualityBudgetConfig.cs`, default
   asset `QualityBudgetConfig.asset`) — data-driven per-platform `standalone`/`pcvr` budgets.
4. **Lifecycle / teardown** — objects that allocate GPU resources (textures, render targets) or
   subscribe to events must release/unsubscribe on teardown. No leaked RenderTextures.
5. **Fail-to-error-state** — missing scene, denied permission, or a missing anchor on load must
   fail to a clean, communicated state, never a crash or silent no-op (ADR 0006, 0010).
6. **On-device-is-truth** — a milestone touching passthrough/depth/alignment/anchors is not
   "done" until verified on Quest 3, not just in the Simulator (ADR 0011).
7. **Scope discipline** — furniture and non-MVP decoration do not enter `Painting`; they arrive
   as a separate assembly later (ADR 0008). MVP scope is the milestone list, nothing more.

---

## 9. Traceability

| Architectural element | Source decision |
|-----------------------|-----------------|
| Meta XR SDK / MRUK in `Spatial` | ADR 0001 |
| `IPointerSource` (controller + hands) in `Interaction` | ADR 0002 |
| `OVRInput`/`OVRHand` as the pointer sources' input backend | ADR 0001 (Meta SDK), ADR 0002 |
| Command list + renderer + per-wall texture in `Painting` | ADR 0003 |
| `IPaintCommand` + `PaintHistory` in `Core`; commands carry a surface id, not a surface | ADR 0003 (command list), ADR 0007 (one global stack), ADR 0006 (id survives save/load) |
| `IPaintCanvas` — the `(u,v)` drawing seam a command renders itself into (`Core`) | ADR 0003 ("a renderer must translate commands → texture"; adding a tool adds a command type); the canvas owns the `(u,v)`→texel mapping so resolution stays config, ADR 0004 |
| Canvas budget (256/1024, data-driven) | ADR 0004 |
| Depth occlusion + z-offset | ADR 0005 |
| `com.unity.xr.meta-openxr` as the depth *provider* under the OpenXR runtime | ADR 0017 (provider package), ADR 0005 (the occlusion decision), ADR 0016 (the runtime it must not reverse) |
| `EnvironmentOcclusion` in `App` — occlusion on/off + quality from the budget | ADR 0005, ADR 0004/0012 (config-driven), *placement in `App` is an organizational choice: it switches global render state and keeps the depth SDK out of `Painting`* |
| JSON persistence keyed by anchor UUID in `Core` | ADR 0006 |
| Single global undo/redo stack in `Core` | ADR 0007 |
| 5-assembly layout + future `Furniture` | ADR 0008 |
| Wrist-anchored uGUI palette in `App` | ADR 0009 |
| `PaintColorPalette` — the palette's colour set as a config asset (`App`) | ADR 0009 (the palette itself); the config-asset shape follows §8.3's precedent from ADR 0004. *Placement in `App` is an organizational choice: the swatch list is the UI's menu, not part of the paint model — a command already carries a `Color32` (ADR 0003) and needs no colour type of its own* |
| `IPaintableSurface` + manual-plane fallback | ADR 0010 |
| No-scene guidance panel in `App` (world-space uGUI) | ADR 0010 (guidance), ADR 0009 (uGUI) |
| Editor/Simulator + on-device verification | ADR 0011 |
| Data-driven budgets for PCVR | ADR 0012 |
| Unity MCP + Meta XR MCP Extension (tooling) | ADR 0013 |
| URP render pipeline + passthrough constraints | ADR 0014 |
| `dev`-based git workflow, ADR gate | ADR 0015 |
| Selection grouping in `Interaction`; composition root in `App` | *organizational choice* |
| Controller-vs-hand arbitration behind one `IPointerSource` (§3) | ADR 0002 (one interface); rules are an *organizational choice* |
