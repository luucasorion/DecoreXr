# ADR 0013 — MCP tooling: coplayDev Unity MCP as the Meta XR setup path

- Status: Accepted
- Date: 2026-08-10
- Deciders: lucas.orion
- Updated: 2026-08-10 — corrected the setup endpoint/path (see Change log)

## Context
Editor setup for MR (XR rig, passthrough, Android manifest, permissions, MRUK, Interaction SDK,
Building Blocks) is error-prone by hand. An MCP-driven workflow can automate it reproducibly.

## Decision
Meta-XR Editor setup goes through the **base coplayDev Unity MCP** (`com.coplaydev.unity-mcp`),
served over HTTP at **`http://127.0.0.1:8080`**. This is the confirmed, working endpoint for
this environment; **confirm it before any Unity MCP tool call**.

Building Blocks (camera rig, passthrough, etc.) are installed through this endpoint via
`execute_code` invoking `Meta.XR.BuildingBlocks.Editor.BlockData.ContextMenuInstall()` (or the
server's `execute_custom_tool` where an equivalent custom tool is registered), against the Meta
XR SDK (`com.meta.xr.sdk.all`, which pulls in `com.meta.xr.sdk.core` + MRUK per ADR 0001).

The **Meta XR Unity MCP Extension** (`com.meta.xr.unity-mcp.extension`) remains installed and its
`meta_*` tools **may** be used when exposed, but it is **optional/supplementary** — it is not a
required path and setup must not depend on it being connected.

## Rationale
The coplayDev endpoint is the one actually reachable and scriptable here; routing Meta XR setup
through it keeps the workflow reproducible without depending on a second MCP server's approval
state. The Meta SDK's own Building Block installers do the correct rig/passthrough/manifest work,
so driving them through `execute_code` gets the Meta-blessed setup without hand-configuration.

## Consequences
- Positive: consistent, repeatable MR project setup against a single confirmed endpoint; no hard
  dependency on the Meta XR MCP Extension being connected.
- Negative / risks: dependency on the coplayDev MCP server running at `127.0.0.1:8080`; the
  endpoint must be confirmed before tool calls. Building-block installs rely on Meta SDK editor
  APIs (`BlockData`) whose namespaces/signatures can shift across SDK versions.

## Alternatives Considered
- **Meta XR MCP Extension as the required path** — rejected: its `meta_*` tools were not reliably
  exposed/approved in this environment; making it mandatory blocked setup. Kept as optional.
- **Hand-configure the Editor** — rejected: error-prone and non-reproducible.

## Notes
These are editor/tooling dependencies, not runtime dependencies. Meta XR SDK/MRUK runtime choice
is ADR 0001; URP passthrough render settings are ADR 0014; manifest/permissions are M0-T3.

## Change log
- 2026-08-10 (initial): base Unity MCP + Meta XR MCP Extension; "Meta-XR Editor setup goes through
  the Meta XR MCP Extension."
- 2026-08-10 (update): endpoint corrected to the coplayDev Unity MCP at `http://127.0.0.1:8080` as
  the primary Meta XR setup path (via `execute_code` + `BlockData.ContextMenuInstall()`); the Meta
  XR MCP Extension demoted to optional/supplementary.
