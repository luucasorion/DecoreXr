# ADR 0017 — `com.unity.xr.meta-openxr` as the Depth API provider

- Status: Accepted
- Date: 2026-08-17
- Deciders: lucas.orion

## Context
ADR 0005 accepts **environment depth occlusion** (Meta Depth API) for the MVP; M4-T1 is the task
that turns it on. ADR 0016 locks the XR runtime to the **OpenXR plugin**
(`com.unity.xr.openxr`) with the Meta XR feature group, and explicitly rejects the legacy Oculus
XR plugin.

Those two decisions do not currently meet. Meta's `EnvironmentDepthManager` only constructs a
depth provider behind one of two version defines
(`Library/PackageCache/com.meta.xr.sdk.core@*/Scripts/EnvironmentDepth/Meta.XR.EnvironmentDepth.asmdef`):

| Define | Comes from | Status here |
|---|---|---|
| `XR_OCULUS_4_2_0_OR_NEWER` | `com.unity.xr.oculus` ≥ 4.2.0 | rejected by ADR 0016 |
| `OPEN_XR_META_2_1_OR_NEWER` | `com.unity.xr.meta-openxr` ≥ 2.1.0 | **not installed** |

With neither define set, `EnvironmentDepthManager.CreateProvider()` falls through to
`DepthProviderNotSupported`, `IsSupported` is permanently false, and the SDK's own Occlusion
Building Block refuses to install (`OcclusionBlockData.cs` guards on the same two defines and
throws `InstallationCancelledException`). ADR 0005 is therefore unimplementable as the project
stands — not for a code reason, but for a missing package.

`DepthProviderOpenXR` additionally requires the **"Meta Quest: Occlusion"** OpenXR feature to be
enabled, or it logs an error and reports the platform unsupported. That feature is currently off
in `Assets/XR/Settings/OpenXR Package Settings.asset`.

## Decision
Add **`com.unity.xr.meta-openxr` ≥ 2.1.0** as a runtime package dependency, and enable the
**"Meta Quest: Occlusion"** OpenXR feature for the Android build target.

The package is the depth *provider* only: no AR Foundation type enters DecoreXR code. Our
assemblies keep talking to `Meta.XR.EnvironmentDepth` (`EnvironmentDepthManager`,
`OcclusionShadersMode`) and to Meta's `META_DEPTH_*` shader macros, exactly as ADR 0005 intends;
the new package sits underneath, supplying the `MetaOpenXROcclusionSubsystem` that
`DepthProviderOpenXR` needs. Editor-side installation goes through the Unity MCP path (ADR 0013),
not hand-configuration.

Whether occlusion is actually on at runtime stays **data-driven config**, not a build-time fact:
`QualityBudgetConfig.Budget.occlusionEnabled` (ADR 0004, ADR 0012) already exists for this and
remains the switch, per architecture §8.3.

## Rationale
It is the only path that satisfies ADR 0005 without reversing ADR 0016. The alternative define
comes from the plugin ADR 0016 rejected on forward-looking grounds, and ADR 0016's own rationale
named "a cleaner path to features like Depth API occlusion (ADR 0005)" as a reason to choose
OpenXR — this package is that path made explicit. It is also Unity's and Meta's supported
combination for depth under OpenXR, so the Building Block installer works as designed rather than
needing a hand-rolled substitute.

## Consequences
- Positive: ADR 0005 becomes implementable; the Meta Occlusion Building Block installs cleanly;
  the runtime decision in ADR 0016 stands unchanged; no AR Foundation surface leaks into
  DecoreXR assemblies.
- Negative / risks:
  - Adds `com.unity.xr.meta-openxr` and, transitively, **AR Foundation + ARSubsystems** to the
    build — a real APK-size and dependency-surface cost for one feature.
  - Depth occlusion costs GPU headroom on standalone Quest 3, which ADR 0005 already flagged and
    ADR 0004's budget governs. Whether the cost is affordable is only knowable on device
    (ADR 0011) — that is M4-T2's job, and the honest failure mode is turning
    `occlusionEnabled` off in the budget config rather than reverting this ADR.
  - Two OpenXR feature toggles must now stay correct (Meta XR Feature + Meta Quest: Occlusion);
    a wrong toggle surfaces only on device (ADR 0011).
  - Installing the package triggers a domain reload and rewrites `packages-lock.json`. It also
    edits `ProjectSettings.asset` on our behalf: it sets `runInBackground` to true — which XR
    Plug-in Management wants so an OpenXR session survives losing focus, and which is right for a
    passthrough app the user can step out of — and adds the `USE_INPUT_SYSTEM_POSE_CONTROL` and
    `USE_STICK_CONTROL_THUMBSTICKS` defines its input wiring expects. Both are kept rather than
    reverted, and are recorded here so they are not mistaken later for an unexplained hand-edit.
  - The transitive pull is wider than AR Foundation alone: `com.unity.xr.compositionlayers` comes
    with it and generates `Assets/CompositionLayers/` and the XR Simulation assets under
    `Assets/XR/`. Generated, but committed, so the project resolves the same way for everyone.

## Alternatives Considered
- **`com.unity.xr.oculus` ≥ 4.2.0 (the legacy Oculus XR plugin)** — rejected: it would satisfy
  the define, but only by reversing ADR 0016 and swapping the whole XR runtime back to the
  backend Meta is steering projects away from. A large regression to enable one feature.
- **Hand-roll occlusion from the depth texture without the package** — rejected: there is no
  depth texture to sample. The provider that acquires it is precisely what the package supplies;
  without it the Depth API has no data source at all.
- **Drop occlusion from the MVP** — rejected: this was already the rejected alternative in
  ADR 0005, where the user chose occlusion for its painting-feedback value (hands and controller
  correctly occluding paint), not merely for realism. Nothing has changed that trade.
- **Leave the paint quad's ~5mm z-offset (ADR 0005) as the only depth handling** — rejected: the
  offset only prevents z-fighting against the wall. It does nothing about a hand in front of the
  wall drawing behind the paint, which is the feedback problem ADR 0005 set out to fix.

## Notes
Runtime SDK choice is ADR 0001; the occlusion decision itself is ADR 0005; the budget that gates
it is ADR 0004/0012; the XR runtime is ADR 0016; the MCP install path is ADR 0013; on-device
verification is ADR 0011. Consumed by M4-T1 (enable occlusion) and M4-T2 (on-device tuning of
z-offset and depth bias).
