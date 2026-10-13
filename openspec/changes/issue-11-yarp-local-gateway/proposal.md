# Proposal

> Tracks GitHub issue [hexmasternl/battleship#11](https://github.com/hexmasternl/battleship/issues/11).

## Why

The frontend has no way to reach the backend yet. Every module runs its own API on its own port, which Aspire picks at run time, so the frontend would have to know each one. A single gateway endpoint lets the frontend use one base URL. It also makes the module route prefixes an explicit contract, which needs an owner as the number of modules grows.

## What Changes

- The Aspire AppHost adds a YARP gateway resource. It waits for the module APIs, then routes each request by its first path segment to the module that owns that prefix. The path is passed through unchanged.
- One route table in the AppHost lists every public prefix per module: `players` to the Players API, `games` to the Games API. A module that owns more than one prefix lists each one explicitly. Prefixes not in the table, including internal endpoints such as Dapr subscription routes and `/smoke/*`, are not reachable through the gateway.
- The Players and Games APIs serve their public endpoints under their own prefix (`/players/...`, `/games/...`). The existing smoke and weather-forecast endpoints stay internal.
- The Angular app becomes an Aspire resource that waits for the gateway and receives the gateway URL as an environment variable. At start-up it turns that variable into a runtime config file that the app loads before it bootstraps, because `ng serve` cannot read environment variables.
- An HTTP interceptor in the frontend rewrites requests that start with `/api/` to `<gateway-base-url>/<rest of path>` and leaves all other requests alone.
- A new agent skill, `yarp-gateway-routes`, mirrored for Claude Code and Copilot, owns the route table. It notices when a backend module is added, writes a route proposal (module, API resource, every prefix) and adds the route only after the user approves.

## Capabilities

### New Capabilities
- `local-gateway`: the single backend entry point of the local development environment. Covers the gateway resource, start-up ordering, prefix-based routing to module APIs and the explicit module route table.
- `frontend-backend-access`: how the frontend finds and calls the backend. Covers the runtime configuration that carries the gateway URL and the `/api` request rewrite.

### Modified Capabilities

## Impact

- **Aspire**: `AppHost.cs` gains the gateway and the frontend resources. The AppHost project gains the `Aspire.Hosting.Yarp` and `Aspire.Hosting.JavaScript` packages and a route table type.
- **Players, Games**: public endpoints move under their module prefix (no public endpoints exist yet beyond smoke and weather-forecast).
- **Frontend**: a runtime config loader, a backend-URL provider, an `/api` interceptor, `provideHttpClient` in `app.config.ts`, a start-up script that writes the config file, plus unit tests.
- **Agent configuration**: a new skill under `.claude/skills/yarp-gateway-routes/` and `.github/skills/yarp-gateway-routes/`.
- **Related changes**: `issue-7-user-identification` plans `/users/me` and `/tokens` on the Players API. It needs to put them under `/players` or declare extra prefixes through the new skill. `issue-4-server-sent-events` adds its `/events` route through the skill and can drop its direct CORS connection.
- **Out of scope**: production ingress.
