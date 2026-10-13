# Design

## Context

`AppHost.cs` runs RabbitMQ, the Dapr `pubsub` component and the Games and Players APIs, each with a Dapr sidecar. The Angular app (`src/Frontend`, Angular 22, zoneless) runs on its own with `ng serve`. It has no `HttpClient`, no interceptor and no backend URL. Neither API maps public endpoints yet. They only map `/smoke/*` (development only), `/weatherforecast`, the Dapr subscribe handler and the ServiceDefaults health endpoints. Aspire picks ports at run time, so nothing outside the AppHost can hard-code a backend address. See proposal.md for the motivation.

## Goals / Non-Goals

**Goals:**
- One gateway URL for the frontend, handed over by Aspire on each run.
- One place in the code that says which module owns which prefix, owned by an agent skill.

**Non-Goals:**
- Production ingress, TLS termination, auth or rate limiting at the gateway.
- Load balancing over several instances of a module.
- Routing for `/events`. The Events API does not exist yet. Its route is added through the new skill when `issue-4-server-sent-events` lands.

## Decisions

**Gateway: the Aspire YARP integration (`Aspire.Hosting.Yarp`).** `builder.AddYarp("gateway")` runs the YARP container image, and `WithConfiguration(yarp => yarp.AddRoute(path, api))` points routes at project resources through service discovery. The gateway calls `.WaitFor()` on each API it routes to. Alternative considered: a hand-written YARP ASP.NET project in the solution. It was rejected because it adds a project, a Dockerfile and configuration we would have to maintain, and it gains nothing for local development. Docker is already required for Dapr and RabbitMQ, so the container adds no new prerequisite.

**Route table: one static class in the AppHost, `GatewayRoutes`.** It holds a list of `ModuleRoute(string Module, string ApiResourceName, IReadOnlyList<string> Prefixes)` entries:
- `players` -> `hexmaster-battleship-players-api`, prefixes `["players"]`
- `games` -> `hexmaster-battleship-games-api`, prefixes `["games"]`

The AppHost loops over it and adds `/{prefix}/{**catch-all}` per prefix. It adds no path transform, so the API sees the original path. A guard method rejects an empty, duplicate, non-lowercase or reserved prefix (`smoke`, `dapr`, `health`, `alive`, `integration-events`, `openapi`) at start-up. Keeping the table as data, separate from `AppHost.cs`, gives the skill one file to read and edit, and lets a unit test check it. Alternative considered: routes from `appsettings.json` in the YARP container. It was rejected because the destinations are Aspire resources, and the C# configuration resolves them through service discovery.

**No prefix stripping at the gateway.** The prefix is part of each API's public contract (`/players/...`). The gateway and the APIs therefore agree on paths, and production ingress can copy the same table later. Internal routes stay reachable on the API's own port for Dapr and diagnostics, but the gateway never sees them, because only declared prefixes get routes.

**Frontend resource: `Aspire.Hosting.JavaScript`.** `builder.AddJavaScriptApp("frontend", "../../../Frontend", "start:aspire")` runs the app, with `.WithHttpEndpoint(env: "PORT")`, `.WithEnvironment("BACKEND_GATEWAY_URL", gateway.GetEndpoint("http"))` and `.WaitFor(gateway)`. A new npm script `start:aspire` runs `node scripts/aspire-start.mjs`. That script writes `public/config.json` (`{ "backendBaseUrl": "<BACKEND_GATEWAY_URL>" }`), then starts `ng serve --port <PORT>` so it works on Windows, macOS and Linux shells alike. It exits non-zero with a clear message when `BACKEND_GATEWAY_URL` is missing. `public/config.json` is git-ignored. Alternative considered: a build-time `environment.ts`. It was rejected because the port changes every run and the value would be baked into the bundle. Alternative considered: the `ng serve` proxy (`proxy.conf.js` reading the environment). It was rejected because the issue asks for the frontend to address the gateway directly through an interceptor, and the dev-server proxy buffers streaming responses, which matters for SSE later.

**Runtime config in the app.** `provideAppInitializer` fetches `/config.json` with `fetch` before any `HttpClient` call (the interceptor must not intercept its own config load). The result goes into a `BackendConfig` service exposed as a read-only signal. A missing file or key logs one `console.error` naming `backendBaseUrl` and leaves the value `null`.

**Interceptor: a functional `HttpInterceptorFn`, `apiGatewayInterceptor`,** registered via `provideHttpClient(withInterceptors([...]))`. A URL that starts with `/api/` becomes `${backendBaseUrl}/${rest}`, with the query kept. With no base URL it returns an error observable (`throwError`) instead of sending the request. Any other URL passes through. The `angular-frontend` agent builds this with unit tests.

**CORS: the frontend origin is allowed on the APIs.** The browser calls the gateway on a different port than the one the app is served from, so the API responses need CORS headers. YARP forwards preflight `OPTIONS` requests, so the API answers them. ServiceDefaults gains `AddGatewayCors()`/`UseGatewayCors()`. The policy allows the origins in configuration key `Gateway:AllowedOrigins`, and the AppHost sets that key on each API to the frontend's endpoint. It applies in Development only. Alternative considered: CORS in the YARP container. It was rejected because the Aspire YARP integration has no first-class CORS policy configuration, and response-header transforms cannot answer preflights. A future production ingress on the same origin as the frontend makes this unnecessary.

**Skill: `yarp-gateway-routes`, in `.claude/skills/` and `.github/skills/`, kept identical with the `sync-agent-config` check.** It applies whenever someone adds a backend module (a new directory under `src/` with an `*.Api` project), adds an `AddProject<...Api>` to the AppHost, maps a new public endpoint prefix, or edits `GatewayRoutes`. Its procedure:
1. Diff the `*.Api` projects in the AppHost against `GatewayRoutes`.
2. For each gap, write a route proposal: module, API resource name, every prefix (stated one by one when there are several), and the endpoints each prefix covers. Present it to the user and ask for approval.
3. Only after approval, edit `GatewayRoutes`. Keep the module's endpoints under the approved prefixes, run the route-table tests, and note the routes in the change's tasks.

It never routes internal endpoints, and never adds, renames or removes a prefix without explicit approval. The skill only adds routes to the gateway it maintains (decision 1); it never installs the gateway itself.

## Risks / Trade-offs

- [The YARP container cannot reach host-run projects on some Docker setups] -> Aspire's service discovery resolves project endpoints for containers through its own host address. Verify on Windows during the first task. If it fails, fall back to a YARP project resource with the same route table.
- [CORS widens what the APIs accept in Development] -> The allowed origin is exactly the frontend endpoint of the current run, set by the AppHost, and the policy is off outside Development.
- [Other in-flight changes use routes outside the prefixes (`/users/me`, `/tokens`, `/events`)] -> The proposal flags them. The skill makes every new prefix an explicit, approved decision when those changes are applied.
- [`config.json` left over from an earlier run points at a stale port] -> `aspire-start.mjs` rewrites it on every start.

## Migration Plan

Local development only. Developers start the solution through the AppHost as before. Running `ng serve` on its own without Aspire still works for UI-only work, but `/api` calls then fail with the logged configuration error. Rollback means reverting the commit.
