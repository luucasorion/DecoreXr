# ADR 0002 — Dual input (controller + hands) behind a pointer abstraction

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion (via grilling session)

## Context
The user must point at and paint on walls. Options: controller ray only, hand-tracking only,
or both. Painting needs steady pointing and a clear pen-down / pen-up signal.

## Decision
Support **both controller ray and hand-pinch**, feeding a single pointer interface
(`IPointerSource`) that the painting pipeline consumes.

## Rationale
Controller is precise and demo-friendly (trigger = clean pen down/up); hands are natural in MR.
Routing both through one interface lets either drive painting without the paint engine knowing
which is active.

## Consequences
- Positive: input source is swappable; hands can be added/tuned without touching painting.
- Negative / risks: two input backends to maintain; hand-tracking fine strokes are less precise.
  Mitigated by treating controller as the primary path for the MVP.

## Alternatives Considered
- **Controller only** — rejected: user wants hands too.
- **Hands only** — rejected: too imprecise for fine strokes as the sole input.

## Notes
`IPointerSource` lives in the `Interaction` assembly (ADR 0008).
