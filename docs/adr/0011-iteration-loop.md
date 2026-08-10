# ADR 0011 — Iteration loop: Editor + Simulator, on-device is truth

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion (via grilling session)

## Context
Passthrough, depth occlusion, and anchor persistence only behave correctly on real hardware,
but APK deploy latency is too slow for every logic/UI tweak.

## Decision
Use **Unity Editor + Meta XR Simulator** for painting logic and UI iteration, and **periodic
on-device APK builds on Quest 3** to verify passthrough, depth, alignment, and anchor
persistence. On-device behavior is the source of truth for those concerns.

## Rationale
Fast inner loop for the bulk of the work; hardware truth where the Simulator can't be trusted.

## Consequences
- Positive: fast iteration without sacrificing MR correctness.
- Negative / risks: Simulator can mask MR-only bugs — spatial milestones must be signed off
  on-device before being considered done (architecture §8 "on-device-is-truth").

## Alternatives Considered
- **On-device only** — rejected: too slow for logic/UI.
- **Simulator only** — rejected: can't validate passthrough/depth/anchors.

## Notes
Future PCVR mode (ADR 0012) adds a third run target over Meta Quest Link.
