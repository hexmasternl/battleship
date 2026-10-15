---
name: yarp-gateway-routes
description: Owns the YARP gateway route table of the local development environment and its Azure deployment copy (`infra/gateway-routes.json`). Use when a backend module or `*.Api` project is added, when an `AddProject<...Api>` is added to the Aspire AppHost, when an API maps an endpoint under a new first path segment, when `GatewayRoutes.cs` is edited, or when asked "which prefixes does the gateway route", "add a gateway route" or "is the gateway in sync". Proposes every new route prefix and waits for approval before adding it.
---

# YARP gateway routes

The AppHost runs a YARP resource named `gateway`. It is the single backend endpoint the frontend talks to. The gateway routes a request by its first path segment, the **prefix**, to the module API that owns it. It passes the path through unchanged, so `/users/me` reaches the Users API as `/users/me`.

Every route comes from one table: `GatewayRoutes` in `src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost/GatewayRoutes.cs`. This skill owns that table. Nothing else adds, renames or removes a prefix.

The Azure deployment runs the same gateway as a Container App configured from `infra/gateway-routes.json`. That file mirrors the table: the same modules and prefixes, with `app` set to the Container App name (the API resource name without `hexmaster-battleship-`). `GatewayRoutesFileTests` fails when the two disagree, so every route change edits both.

## Rules

- **Every prefix is explicit.** Each `ModuleRoute` entry names the module, the API resource name used in `AppHost.cs`, and the full list of its prefixes. A module that owns several prefixes lists each one; never infer a prefix from a module name, and never add a wildcard route.
- **Approval first.** Adding, renaming or removing a prefix changes the public contract between frontend and backend. Always present a route proposal (below) and wait for the user's explicit approval before editing `GatewayRoutes`. An approval covers exactly the prefixes in that proposal.
- **Internal endpoints never get a route.** Dapr subscription routes (`/dapr/*`, `/integration-events` and every other `[Topic]` endpoint), smoke and diagnostics endpoints (`/smoke/*`), health endpoints (`/health`, `/alive`), OpenAPI documents (`/openapi/*`), the Scalar API reference (`/scalar/*`) and template leftovers such as `/weatherforecast` stay reachable on the API's own port only. `GatewayRoutes` rejects the reserved ones at start-up; do not work around that guard.
- **Prefixes are lowercase, one path segment, unique across all modules.** Two modules never share a prefix.
- **The API serves under its prefixes.** Every public endpoint of a module maps under one of its declared prefixes, for example `app.MapGroup("/users")`. If an endpoint sits outside its module's prefixes, the fix is moving the endpoint or proposing an extra prefix, never routing a bare path.
- **No path transforms.** Routes are `/{prefix}/{**catch-all}` to the module API without `WithTransformPathRemovePrefix` or similar. The frontend's `/api` prefix is stripped by its own interceptor before the request reaches the gateway.

## When this applies

Run the detection whenever one of these happens, including as part of another change:

1. A new directory under `src/` with a `*.Api` project appears (a new module).
2. `AppHost.cs` gains an `AddProject<Projects.*_Api>(...)` call.
3. A module API maps an endpoint whose first path segment is not one of that module's prefixes (a new `MapGroup`, `MapGet`, `MapPost`, ... at a new root).
4. Someone asks to add, rename or remove a gateway route, or edits `GatewayRoutes.cs`.

## Detection

1. List the API projects the AppHost runs: every `AddProject<Projects.<Name>>("<resource-name>")` in `AppHost.cs`.
2. List the modules in `GatewayRoutes`.
3. For each module API, list the first path segments of its public endpoints: search its project for `MapGroup(`, `MapGet(`, `MapPost(`, `MapPut(`, `MapPatch(`, `MapDelete(` and `MapMethods(`, and drop the internal ones listed under Rules.
4. Report the gaps:
   - an API in the AppHost with no `GatewayRoutes` entry
   - a `GatewayRoutes` entry whose API resource no longer exists
   - a public endpoint prefix not declared for its module
   - a declared prefix that no endpoint of the module uses (allowed while the module is being built, but say so)
   - a module or prefix in `infra/gateway-routes.json` that differs from `GatewayRoutes`

If there are no gaps, say the gateway is in sync and stop.

## Route proposal

For every gap, write one proposal and present it to the user. Do not edit anything yet.

```markdown
### Gateway route proposal: <module>

| | |
|---|---|
| Module | `<module>` (`src/<Module>`) |
| API resource | `<resource-name>` (`Projects.<Name>`) |
| Prefixes | `/<prefix-1>`, `/<prefix-2>` |
| Change | add module / add prefix / rename `/<old>` to `/<new>` / remove `/<prefix>` |

**Endpoints per prefix**
- `/<prefix-1>`: `GET /<prefix-1>/...`, `POST /<prefix-1>/...`
- `/<prefix-2>`: ...

**Not routed (internal)**: `<endpoints left out and why>`

**Frontend calls**: `/api/<prefix-1>/...` -> `<gateway>/<prefix-1>/...`
```

List each prefix separately, even when a module has only one. When a module has several prefixes, say for each one why it is not under the module's main prefix. Then ask: "Approve these gateway routes?" and wait.

In Claude Code ask with `AskUserQuestion` (approve / change / reject). In other agents ask in chat and wait for the answer.

## After approval

1. Edit `GatewayRoutes.cs`: add or update the `ModuleRoute` entry with exactly the approved prefixes, in the order they were proposed. Make the same edit in `infra/gateway-routes.json`. A new module there also needs its Container App in `infra/main.bicep`, so the deployed gateway has a destination.
2. If the module API is new to the AppHost, make sure its resource builder is the one `GatewayRoutes` resolves by resource name, and that the gateway `WaitFor`s it (the AppHost loop does this for every entry).
3. Move or create the module's public endpoints under the approved prefixes if the proposal said so.
4. Run the route-table tests: `dotnet test --project src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.Tests`. Add a test case for each new prefix.
5. Record the routes in the change you are working on: a task line in its `tasks.md` and, when the change has specs, a scenario that a request under the new prefix reaches the module.

A rejected or changed proposal goes back to the Route proposal step. Never add a prefix that was not approved, even if it would make a build or a test pass.
