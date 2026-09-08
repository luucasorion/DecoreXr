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
  It writes and reads the files but does not decide which saved walls are in the room — that
  needs anchors, so it belongs to `Spatial` (ADR 0006's own note). Two consequences of the split
  live here: the store removes no file until something has loaded, since before that a wall with
  no paint is indistinguishable from one nobody has looked for; and a wall the loader reports as
  absent is *retained*, so saving the room the user is in never erases a room they are not.
- **Spatial** — wraps MRUK: requests scene permission, loads walls as `MRUKAnchor`, exposes
  them (and manual fallback planes) as `IPaintableSurface` with its plane, rectangle and `(u,v)`
  mapping, and resolves anchor UUIDs. The only assembly that references Meta scene APIs.
  Because resolving a UUID is its job, it is also where saved paint is matched back onto the room:
  `PaintReattacher` reads through `Core`'s store, restores the commands whose anchors this room
  has, and reports the rest as belonging to a room that is not here (ADR 0006).
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
- **Painting** — the paint model: command types (fill, brush, eraser, circle, rectangle,
  triangle, diamond, stripe, …), the renderer that draws a command list into a per-wall texture within
  the data-driven budget (ADR 0004), and the per-wall canvas registry keyed by anchor UUID.
- **App** — bootstraps the scene, builds the wrist-anchored uGUI palette (ADR 0009), and wires
  Interaction → Painting → Core through their public interfaces. Owns tool/color selection state.
  It also owns *when* things happen rather than how: saving the room is driven from the app's own
  lifecycle — pausing, quitting, and a short delay after the last paint action — because ADR 0006
  promises the room survives a restart, and on Quest the app is backgrounded and then reclaimed
  with no further warning.
- **Furniture (future)** — 3D `SpatialObject` placement/move/rotate/scale as new
  `IPaintCommand`s sharing the global stack; depends on `Core` + `Spatial`, never on `Painting`.

---

## 4. How Components Communicate

- **Interfaces, one direction.** `App` depends on `Painting`/`Interaction`/`Spatial`/`Core`;
  `Painting` depends on `Core` (+ `IPaintableSurface` from `Spatial`); `Spatial`,
  `Interaction`, `Core` do **not** depend on `App` or `Painting`. No cyclic asmdef references.
- **Commands as the currency.** User actions become `IPaintCommand` instances pushed onto the
  Core stack; undo/redo moves that stack's done-mark rather than editing it, and asks `Painting`
  to re-render only the affected wall.
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
9. Undo/redo moves the done-mark along the Core stack (undone commands are kept, not deleted);
   `Painting` re-renders the affected wall, and `App` names what was undone — including when it
   happened on a wall the user is not looking at (ADR 0007).
10. On save, `Core` serializes each wall's command list to JSON keyed by anchor UUID.
11. On load, `Spatial` reads those files back and keeps the commands whose anchor this room has,
    restoring them into the history in the order they were originally painted; `Painting` redraws
    each of those walls once. Commands whose anchor is missing are not restored and their files are
    left untouched, so paint belonging to another room is neither shown nor destroyed (clean fail —
    ADR 0006, §8.5).

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
| `PaintStore` — one JSON file per anchor UUID under `persistentDataPath` (`Core`) | ADR 0006 (the decision, and the UUID as the key); *a file per wall rather than one for the room is an organizational choice: the filename is the most direct way to be keyed by the UUID, a wall rewrites only its own file, and a corrupt file costs that wall instead of the room* |
| `IPaintCommandCodec` / `IPaintCommandCodecSource` / `PaintCommandCodecRegistry` in `Core`, with the codecs themselves in `Painting` | ADR 0006 (serialization lives in `Core`), ADR 0003 (a tool is a command type), ADR 0008/§4 (`Core` cannot name `Painting`'s types); *the seam is an organizational choice, the same inversion `IPaintCanvas` is: a command type is persisted by a codec that ships beside it, so adding a tool adds a command type and a codec and touches nothing in `Core`* |
| `SavedCommand.Order` — each command's place in the global history, written into its wall's file | ADR 0006 (per-wall files) + ADR 0007 (one global stack); *the two only reconcile if a per-wall entry says where it sat globally, so N files merge back into one ordered history on load rather than into a room grouped by wall* |
| Only *done* commands are persisted; the undone/redo tail is not | ADR 0006 (the file is what the walls look like) + ADR 0007; *reading of ADR 0007 rather than a statement in it: undo/redo is promised within a session, so carrying the undone tail to disk would leave a redo button live on launch offering to restore paint from a session the user has left* |
| `PaintReattacher` in `Spatial` — matches saved anchor UUIDs against the room and restores what is here | ADR 0006 ("anchor UUID lookup in `Spatial`", and its orphaned-anchor risk), ADR 0010 (it asks `IPaintableSurfaceProvider`, so MRUK walls and fallback planes answer alike) |
| `PaintHistory.Restore` + `HistoryRestored` — the whole history replaced in one step, announced once (`Core`) | ADR 0006 (loading), ADR 0003 + §7; *organizational choice: a run of `Push`es would make `Painting` replay a wall once per command restored onto it, so one notification per load is what keeps a reload one rasterization per wall* |
| `PaintPersistence` in `App` — save on pause/quit and a short delay after the last paint action; load left to the reattacher | ADR 0006 (the restart promise); *placement and the choice of triggers are organizational: ADR 0006 says the room survives a restart and says nothing about a button, and Quest backgrounds then reclaims an app with no further warning, so a save that waited to be asked for would mostly not happen. No palette control for it — the app has no input module feeding uGUI, and choosing one is a cross-cutting input decision needing its own ADR (§8.1), so `Save()`/`Load()` are public and wait for it* |
| Orphaned paint is dropped from the history but **retained** on disk (`PaintStore.Retain`, and no deletion before a load) | ADR 0006 (re-running Space Setup orphans anchors — its stated risk) + §8.5; *dropped from the history because a command whose wall is absent would be undoable paint the user has never seen; kept on disk because "no paint on this wall" and "this wall is in another room" look identical from `Core`, and erasing on that guess would lose a room the user still has* |
| Single global undo/redo stack in `Core` | ADR 0007 |
| `IPaintCommand.DisplayName` — the command's own short name for what it did (`Core`) | ADR 0007 (the UI "should indicate what was undone"); the command names itself rather than the UI switching on command types, for the same reason it renders itself — ADR 0003 |
| Undo/redo controls on the wrist palette + the what-was-undone banner in `App` | ADR 0007 (one global stack, and the risk it accepts that undo jumps to another wall), ADR 0009 (the palette they sit on) |
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
