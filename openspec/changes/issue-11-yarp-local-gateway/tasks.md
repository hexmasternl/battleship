# Tasks

## 1. Gateway route ownership skill

- [x] 1.1 Write `.claude/skills/yarp-gateway-routes/SKILL.md` following design.md (triggers; detection of new `*.Api` projects and new public prefixes; a route proposal listing module, API resource and every prefix one by one; explicit user approval before any prefix is added, renamed or removed; never route internal endpoints; edit `GatewayRoutes` and run its tests after approval). Verify the frontmatter description names the triggers.
- [x] 1.2 Mirror the skill to `.github/skills/yarp-gateway-routes/SKILL.md`, wording tool-specific steps conditionally, and verify with the `sync-agent-config` skill's `check-sync.ps1` reporting no drift.
- [x] 1.3 Add a line to `CLAUDE.md` saying that gateway routes are owned by the `yarp-gateway-routes` skill, and verify the Copilot instructions counterpart (if any) carries the same line per the sync check.

## 2. Gateway in the AppHost

- [x] 2.1 Add `Aspire.Hosting.Yarp` to the AppHost project at the version matching Aspire 13.6.1, and verify `dotnet build src/Battleship.slnx` succeeds.
- [x] 2.2 Add `GatewayRoutes` (`ModuleRoute` records for `players` and `games`, plus the prefix guard rejecting empty, duplicate, non-lowercase and reserved prefixes) to the AppHost project.
- [x] 2.3 Create `src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.Tests` (xUnit, Moq, Bogus per house standards), add it to the solution, and add tests for the route table and the guard. Verify `dotnet test` passes with at least 80% coverage of `GatewayRoutes`.
- [x] 2.4 In `AppHost.cs`, keep the API resource builders in variables, add `AddYarp("gateway")` waiting for both APIs, and add `/{prefix}/{**catch-all}` routes from `GatewayRoutes`. Verify with `aspire run` that the dashboard shows `gateway` start after the APIs, that `curl <gateway>/players/x` and `<gateway>/games/x` show up in the matching API's traces with the path unchanged, and that `<gateway>/smoke/publish` and `<gateway>/unknown` return 404 without reaching an API.

## 3. Module APIs and CORS

- [x] 3.1 Add `AddGatewayCors()` / `UseGatewayCors()` to ServiceDefaults (Development only, origins from `Gateway:AllowedOrigins`), call them from the Players and Games APIs, and add unit tests for the policy. Verify `dotnet test` passes.
- [x] 3.2 Confirm neither API maps a public endpoint outside its declared prefix (smoke, weather-forecast and Dapr routes stay internal), and record the check in the task commit.

## 4. Frontend runtime configuration and interceptor (angular-frontend agent)

- [x] 4.1 Add `scripts/aspire-start.mjs` and the `start:aspire` npm script that writes `public/config.json` from `BACKEND_GATEWAY_URL` and runs `ng serve --port $PORT`. Git-ignore `public/config.json`, and verify that running it without the variable exits non-zero with a clear message.
- [x] 4.2 Add the `BackendConfig` service, which loads `/config.json` with `provideAppInitializer` into a read-only signal and logs a clear error when the file or key is missing. Add unit tests for the loaded, missing-file and missing-key cases, and verify `ng test` passes.
- [x] 4.3 Add `apiGatewayInterceptor` and register `provideHttpClient(withInterceptors([apiGatewayInterceptor]))` in `app.config.ts`. Add unit tests covering `/api/players/me?x=1` rewritten to `<base>/players/me?x=1`, a non-api URL left untouched, an absolute URL left untouched, and a missing base URL producing an error without sending a request. Verify `ng test` passes.
- [ ] 4.4 Document in `src/Frontend/README.md` how backend calls work (`/api/<module>/...`, runtime config, starting through Aspire), and verify the documented command runs.

## 5. Frontend in the AppHost

- [ ] 5.1 Add `Aspire.Hosting.JavaScript` and `AddJavaScriptApp("frontend", ...)` with the `start:aspire` script, `WithHttpEndpoint(env: "PORT")`, `BACKEND_GATEWAY_URL` from the gateway endpoint and `WaitFor(gateway)`. Pass the frontend endpoint to each API as `Gateway__AllowedOrigins`. Verify the dashboard shows the frontend start after `gateway` and the generated `config.json` holds the gateway URL.

## 6. Integration check

- [ ] 6.1 With `aspire run`, open the frontend and, from the browser console, run `fetch('/api/players/x')` through the app's `HttpClient` (or a temporary test call in a unit test harness). Verify the request reaches the Players API through the gateway with no CORS error, that navigating to `/games` causes no gateway request, and that `dotnet build`, `dotnet test` and `ng test` all pass.
