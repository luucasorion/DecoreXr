# ADR 0013 — MCP tooling: base Unity MCP + Meta XR MCP Extension

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion

## Context
Editor setup for MR (XR rig, passthrough, Android manifest, permissions, MRUK, Interaction SDK,
Building Blocks) is error-prone by hand. An MCP-driven workflow can automate it reproducibly.

## Decision
Add the **base Unity MCP** (`com.coplaydev.unity-mcp`) and the **Meta XR Unity MCP Extension**
(`com.meta.xr.unity-mcp.extension`) to `Packages/manifest.json`. Meta-XR Editor setup goes
through the Meta XR MCP Extension, not hand-configuration.

## Rationale
Reproducible, scriptable Editor setup; the Meta extension knows the correct rig/passthrough/
manifest/permission steps for the Meta stack (ADR 0001).

## Consequences
- Positive: consistent, repeatable MR project setup; less manual misconfiguration.
- Negative / risks: dependency on MCP servers running; the endpoint must be confirmed before
  tool calls. If `meta_*` tools aren't exposed, install Building Blocks via `execute_code` +
  `BlockData.ContextMenuInstall()`.

## Alternatives Considered
- **Hand-configure the Editor** — rejected: error-prone and non-reproducible.

## Notes
This environment used the Unity MCP over an HTTP server on port **8080**; confirm the endpoint
before calling Unity MCP tools. These are editor/tooling dependencies, not runtime dependencies.
