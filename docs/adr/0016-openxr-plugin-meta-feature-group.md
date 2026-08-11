# ADR 0016 — OpenXR plugin + Meta XR feature group as the XR runtime

- Status: Accepted
- Date: 2026-08-11
- Deciders: lucas.orion

## Context
The Meta XR SDK (ADR 0001) needs an XR loader plugin registered in Unity XR Plug-in
Management to run on Quest hardware. Unity offers two backends for the Meta stack: the legacy
**Oculus XR Plugin** (`com.unity.xr.oculus`) or the **OpenXR Plugin** (`com.unity.xr.openxr`)
driven by Meta's OpenXR **feature group**. The Meta Project Setup Tool flags the absence of a
loader as a Required fix and recommends OpenXR.

## Decision
Use the **OpenXR Plugin** (`com.unity.xr.openxr`) with the **Meta XR feature group** enabled for
the Android build target, as the XR runtime for DecoreXR. The OpenXR loader is enabled for
Android in XR Plug-in Management; the Meta Quest feature group / features are enabled via the
Meta Project Setup Tool.

## Rationale
OpenXR is Meta's recommended and forward-looking backend for Quest (Meta XR SDK 205+), aligns
with Unity 6 / URP 17, and keeps a cleaner path to features like Depth API occlusion (ADR 0005)
and future portability considerations (ADR 0012) than the legacy Oculus XR plugin. It is the
option the Project Setup Tool actively recommends.

## Consequences
- Positive: modern, Meta-recommended runtime; interaction profiles and feature toggles are
  managed through OpenXR settings; consistent with the current Meta SDK default.
- Negative / risks: OpenXR requires correct feature-group/interaction-profile configuration;
  misconfiguration surfaces only on device (ADR 0011). Installing the plugin triggers domain
  reloads. Adds `com.unity.xr.openxr` as a dependency.

## Alternatives Considered
- **Oculus XR Plugin (`com.unity.xr.oculus`)** — rejected: legacy backend; Meta is steering new
  projects to OpenXR, and it offers no advantage for this MVP.
- **No loader / hand-configure** — not viable: XR Plug-in Management requires a registered loader
  for the build to run on Quest.

## Notes
Runtime SDK choice is ADR 0001; MCP-driven setup path is ADR 0013; URP constraints are ADR 0014.
Enabled during M0-T3 (device setup). On-device verification of the loader/passthrough is M0-T4.
